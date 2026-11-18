using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace ImageSharp.Textures.Vulkan;

/// <summary>
///     Vulkan Compute BC7 block-compression encoder executing DirectXTex's <c>BC7Encode.hlsl</c> SPIR-V kernels.
/// </summary>
public sealed class VulkanBc7Encoder : IBc7HardwareEncoder
{
    private const int BlockBytes = 16;

    private static readonly uint[] Mode137Order = [1u, 3u, 7u];
    private readonly CommandBuffer commandBuffer;
    private readonly CommandPool commandPool;
    private readonly DescriptorPool descriptorPool;
    private readonly DescriptorSetLayout descriptorSetLayout;
    private readonly Device device;
    private readonly Fence fence;
    private readonly Instance instance;
    private readonly PhysicalDevice physicalDevice;
    private readonly Pipeline pipeline137;
    private readonly Pipeline pipeline456;
    private readonly Pipeline pipelineEncode;
    private readonly PipelineLayout pipelineLayout;
    private readonly Queue queue;
    private readonly uint queueFamilyIndex;
    private readonly DescriptorSet set1To2;
    private readonly DescriptorSet set2To1;
    private readonly DescriptorSet set456;
    private readonly DescriptorSet setEncodeDefault;
    private readonly DescriptorSet setEncodeQuick;
    private readonly ShaderModule shader137;
    private readonly ShaderModule shader456;
    private readonly ShaderModule shaderEncode;

    private readonly Lock syncLock = new();
    private readonly Vk vk;
    private ulong blockCapacity;

    private Buffer deviceInBuffer;
    private DeviceMemory deviceInMemory;

    private Buffer deviceOutBuffer;
    private DeviceMemory deviceOutMemory;

    private Buffer err1Buffer;
    private DeviceMemory err1Memory;

    private Buffer err2Buffer;
    private DeviceMemory err2Memory;

    private bool isDisposed;

    private Buffer stagingInBuffer;
    private ulong stagingInCapacity;
    private nint stagingInMapped;
    private DeviceMemory stagingInMemory;

    private Buffer stagingOutBuffer;
    private nint stagingOutMapped;
    private DeviceMemory stagingOutMemory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VulkanBc7Encoder" /> class.
    /// </summary>
    /// <param name="preferSoftwareDevice">
    ///     When <see langword="true" />, prefers a CPU Vulkan implementation (such as <c>llvmpipe</c>) over a discrete or
    ///     integrated GPU.
    /// </param>
    public VulkanBc7Encoder(bool preferSoftwareDevice = false)
    {
        this.vk = Vk.GetApi();
        this.instance = CreateVulkanInstance(this.vk);

        try
        {
            (this.physicalDevice, this.queueFamilyIndex, this.DeviceName, this.DeviceType) =
                SelectPhysicalDevice(this.vk, this.instance, preferSoftwareDevice);
            (this.device, this.queue) = CreateLogicalDevice(this.vk, this.physicalDevice, this.queueFamilyIndex);
            (this.commandPool, this.commandBuffer, this.fence) =
                CreateCommandResources(this.vk, this.device, this.queueFamilyIndex);
            (this.descriptorSetLayout, this.pipelineLayout) =
                CreatePipelineLayouts(this.vk, this.device);
            (this.descriptorPool, this.set456, this.set1To2, this.set2To1, this.setEncodeQuick, this.setEncodeDefault) =
                CreateDescriptorResources(this.vk, this.device, this.descriptorSetLayout);

            this.shader456 = CreateShaderModule(this.vk, this.device, "TryMode456CS.spv");
            this.shader137 = CreateShaderModule(this.vk, this.device, "TryMode137CS.spv");
            this.shaderEncode = CreateShaderModule(this.vk, this.device, "EncodeBlockCS.spv");

            this.pipeline456 =
                CreateComputePipeline(this.vk, this.device, this.pipelineLayout, this.shader456, "TryMode456CS");
            this.pipeline137 =
                CreateComputePipeline(this.vk, this.device, this.pipelineLayout, this.shader137, "TryMode137CS");
            this.pipelineEncode = CreateComputePipeline(this.vk, this.device, this.pipelineLayout, this.shaderEncode,
                "EncodeBlockCS");
        }
        catch
        {
            this.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Gets the name of the selected Vulkan physical device.
    /// </summary>
    public string DeviceName { get; }

    /// <summary>
    ///     Gets the type of the selected Vulkan physical device.
    /// </summary>
    public PhysicalDeviceType DeviceType { get; }

    /// <inheritdoc />
    public bool TryEncode(
        ReadOnlySpan<byte> rgbaSource,
        Span<byte> destination,
        int width,
        int height,
        Bc7EncodingQuality quality)
    {
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        long pixelBytes = (long)width * height * 4;
        int blocksX = (width + 3) >> 2;
        int blocksY = (height + 3) >> 2;
        int totalBlocks = blocksX * blocksY;
        long outputBytes = (long)totalBlocks * BlockBytes;

        if (rgbaSource.Length < pixelBytes || destination.Length < outputBytes)
        {
            return false;
        }

        lock (this.syncLock)
        {
            ObjectDisposedException.ThrowIf(this.isDisposed, this);

            this.EnsureBufferCapacity((ulong)pixelBytes, (ulong)totalBlocks);

            unsafe
            {
                Span<byte> stagingIn = new((void*)this.stagingInMapped, (int)pixelBytes);
                rgbaSource[..(int)pixelBytes].CopyTo(stagingIn);
            }

            this.RecordAndSubmit(
                (uint)width,
                (uint)height,
                (uint)blocksX,
                (uint)totalBlocks,
                (ulong)pixelBytes,
                (ulong)outputBytes,
                quality);

            unsafe
            {
                ReadOnlySpan<byte> stagingOut = new((void*)this.stagingOutMapped, (int)outputBytes);
                stagingOut.CopyTo(destination[..(int)outputBytes]);
            }
        }

        return true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (this.syncLock)
        {
            if (this.isDisposed)
            {
                return;
            }

            this.isDisposed = true;

            unsafe
            {
                if (this.device.Handle != 0)
                {
                    this.vk.DeviceWaitIdle(this.device);
                    this.FreeBuffers();

                    if (this.pipeline456.Handle != 0)
                    {
                        this.vk.DestroyPipeline(this.device, this.pipeline456, null);
                    }

                    if (this.pipeline137.Handle != 0)
                    {
                        this.vk.DestroyPipeline(this.device, this.pipeline137, null);
                    }

                    if (this.pipelineEncode.Handle != 0)
                    {
                        this.vk.DestroyPipeline(this.device, this.pipelineEncode, null);
                    }

                    if (this.shader456.Handle != 0)
                    {
                        this.vk.DestroyShaderModule(this.device, this.shader456, null);
                    }

                    if (this.shader137.Handle != 0)
                    {
                        this.vk.DestroyShaderModule(this.device, this.shader137, null);
                    }

                    if (this.shaderEncode.Handle != 0)
                    {
                        this.vk.DestroyShaderModule(this.device, this.shaderEncode, null);
                    }

                    if (this.descriptorPool.Handle != 0)
                    {
                        this.vk.DestroyDescriptorPool(this.device, this.descriptorPool, null);
                    }

                    if (this.pipelineLayout.Handle != 0)
                    {
                        this.vk.DestroyPipelineLayout(this.device, this.pipelineLayout, null);
                    }

                    if (this.descriptorSetLayout.Handle != 0)
                    {
                        this.vk.DestroyDescriptorSetLayout(this.device, this.descriptorSetLayout, null);
                    }

                    if (this.fence.Handle != 0)
                    {
                        this.vk.DestroyFence(this.device, this.fence, null);
                    }

                    if (this.commandPool.Handle != 0)
                    {
                        this.vk.DestroyCommandPool(this.device, this.commandPool, null);
                    }

                    this.vk.DestroyDevice(this.device, null);
                }

                if (this.instance.Handle != 0)
                {
                    this.vk.DestroyInstance(this.instance, null);
                }
            }

            this.vk.Dispose();
        }
    }

    /// <summary>
    ///     Attempts to create a <see cref="VulkanBc7Encoder" /> if a Vulkan compute device is available on the host.
    /// </summary>
    /// <param name="encoder">The created encoder when successful.</param>
    /// <param name="preferSoftwareDevice">Whether to prefer a CPU Vulkan implementation.</param>
    /// <returns><see langword="true" /> when a Vulkan compute device was initialized; otherwise <see langword="false" />.</returns>
    public static bool TryCreate([NotNullWhen(true)] out VulkanBc7Encoder? encoder, bool preferSoftwareDevice = false)
    {
        try
        {
            encoder = new VulkanBc7Encoder(preferSoftwareDevice);
            return true;
        }
        catch
        {
            encoder = null;
            return false;
        }
    }

    private static Instance CreateVulkanInstance(Vk vk)
    {
        unsafe
        {
            byte* appName = stackalloc byte[32];
            "ImageSharp.Textures"u8.CopyTo(new Span<byte>(appName, 32));

            ApplicationInfo appInfo = new()
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = appName,
                ApplicationVersion = new Version32(1, 0, 0),
                PEngineName = appName,
                EngineVersion = new Version32(1, 0, 0),
                ApiVersion = Vk.Version11
            };

            InstanceCreateInfo createInfo = new()
            {
                SType = StructureType.InstanceCreateInfo, PApplicationInfo = &appInfo
            };

            Check(vk.CreateInstance(&createInfo, null, out Instance instance), "vkCreateInstance");
            return instance;
        }
    }

    private static (PhysicalDevice Device, uint QueueFamily, string Name, PhysicalDeviceType Type) SelectPhysicalDevice(
        Vk vk,
        Instance instance,
        bool preferSoftwareDevice)
    {
        unsafe
        {
            uint count = 0;
            Check(vk.EnumeratePhysicalDevices(instance, &count, null), "vkEnumeratePhysicalDevices");
            if (count == 0)
            {
                throw new InvalidOperationException("No Vulkan physical devices found.");
            }

            PhysicalDevice[] devices = new PhysicalDevice[count];
            fixed (PhysicalDevice* pDevices = devices)
            {
                Check(vk.EnumeratePhysicalDevices(instance, &count, pDevices), "vkEnumeratePhysicalDevices");
            }

            PhysicalDevice bestDevice = default;
            uint bestQueueFamily = uint.MaxValue;
            int bestScore = -1;
            string bestName = string.Empty;
            PhysicalDeviceType bestType = PhysicalDeviceType.Other;

            for (int i = 0; i < devices.Length; i++)
            {
                PhysicalDevice candidate = devices[i];
                vk.GetPhysicalDeviceProperties(candidate, out PhysicalDeviceProperties props);

                uint qCount = 0;
                vk.GetPhysicalDeviceQueueFamilyProperties(candidate, &qCount, null);
                QueueFamilyProperties[] queues = new QueueFamilyProperties[qCount];
                fixed (QueueFamilyProperties* pQueues = queues)
                {
                    vk.GetPhysicalDeviceQueueFamilyProperties(candidate, &qCount, pQueues);
                }

                uint computeFamily = uint.MaxValue;
                for (uint q = 0; q < qCount; q++)
                {
                    if ((queues[q].QueueFlags & QueueFlags.ComputeBit) != 0)
                    {
                        computeFamily = q;
                        break;
                    }
                }

                if (computeFamily == uint.MaxValue)
                {
                    continue;
                }

                int score = preferSoftwareDevice
                    ? props.DeviceType switch
                    {
                        PhysicalDeviceType.Cpu => 100,
                        PhysicalDeviceType.DiscreteGpu => 50,
                        PhysicalDeviceType.IntegratedGpu => 40,
                        _ => 10
                    }
                    : props.DeviceType switch
                    {
                        PhysicalDeviceType.DiscreteGpu => 100,
                        PhysicalDeviceType.IntegratedGpu => 80,
                        PhysicalDeviceType.VirtualGpu => 60,
                        PhysicalDeviceType.Cpu => 20,
                        _ => 10
                    };

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDevice = candidate;
                    bestQueueFamily = computeFamily;
                    bestType = props.DeviceType;
                    bestName = Marshal.PtrToStringUTF8((nint)props.DeviceName) ?? "Unknown";
                }
            }

            if (bestScore < 0)
            {
                throw new InvalidOperationException("No Vulkan physical device with a compute queue was found.");
            }

            return (bestDevice, bestQueueFamily, bestName, bestType);
        }
    }

    private static (Device LogicalDevice, Queue ComputeQueue) CreateLogicalDevice(
        Vk vk,
        PhysicalDevice physicalDevice,
        uint queueFamilyIndex)
    {
        unsafe
        {
            float priority = 1.0f;
            DeviceQueueCreateInfo queueCreateInfo = new()
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = queueFamilyIndex,
                QueueCount = 1,
                PQueuePriorities = &priority
            };

            PhysicalDeviceFeatures features = default;
            DeviceCreateInfo deviceCreateInfo = new()
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &queueCreateInfo,
                PEnabledFeatures = &features
            };

            Check(vk.CreateDevice(physicalDevice, &deviceCreateInfo, null, out Device device), "vkCreateDevice");
            vk.GetDeviceQueue(device, queueFamilyIndex, 0, out Queue queue);
            return (device, queue);
        }
    }

    private static (CommandPool Pool, CommandBuffer Buffer, Fence SubmitFence) CreateCommandResources(
        Vk vk,
        Device device,
        uint queueFamilyIndex)
    {
        unsafe
        {
            CommandPoolCreateInfo poolInfo = new()
            {
                SType = StructureType.CommandPoolCreateInfo,
                QueueFamilyIndex = queueFamilyIndex,
                Flags = CommandPoolCreateFlags.ResetCommandBufferBit
            };

            Check(vk.CreateCommandPool(device, &poolInfo, null, out CommandPool pool), "vkCreateCommandPool");

            CommandBufferAllocateInfo allocInfo = new()
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = pool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = 1
            };

            CommandBuffer cmd;
            Check(vk.AllocateCommandBuffers(device, &allocInfo, &cmd), "vkAllocateCommandBuffers");

            FenceCreateInfo fenceInfo = new() { SType = StructureType.FenceCreateInfo };

            Check(vk.CreateFence(device, &fenceInfo, null, out Fence fence), "vkCreateFence");
            return (pool, cmd, fence);
        }
    }

    private static (DescriptorSetLayout SetLayout, PipelineLayout PipeLayout) CreatePipelineLayouts(
        Vk vk,
        Device device)
    {
        unsafe
        {
            DescriptorSetLayoutBinding* bindings = stackalloc DescriptorSetLayoutBinding[3];
            for (uint i = 0; i < 3; i++)
            {
                bindings[i] = new DescriptorSetLayoutBinding
                {
                    Binding = i,
                    DescriptorType = DescriptorType.StorageBuffer,
                    DescriptorCount = 1,
                    StageFlags = ShaderStageFlags.ComputeBit
                };
            }

            DescriptorSetLayoutCreateInfo layoutInfo = new()
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo, BindingCount = 3, PBindings = bindings
            };

            Check(vk.CreateDescriptorSetLayout(device, &layoutInfo, null, out DescriptorSetLayout setLayout),
                "vkCreateDescriptorSetLayout");

            PushConstantRange pushRange = new()
            {
                StageFlags = ShaderStageFlags.ComputeBit, Offset = 0, Size = (uint)Unsafe.SizeOf<PushConstants>()
            };

            PipelineLayoutCreateInfo pipeLayoutInfo = new()
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = 1,
                PSetLayouts = &setLayout,
                PushConstantRangeCount = 1,
                PPushConstantRanges = &pushRange
            };

            Check(vk.CreatePipelineLayout(device, &pipeLayoutInfo, null, out PipelineLayout pipeLayout),
                "vkCreatePipelineLayout");
            return (setLayout, pipeLayout);
        }
    }

    private static (
        DescriptorPool Pool,
        DescriptorSet Set456,
        DescriptorSet Set1To2,
        DescriptorSet Set2To1,
        DescriptorSet SetEncodeQuick,
        DescriptorSet SetEncodeDefault) CreateDescriptorResources(
            Vk vk,
            Device device,
            DescriptorSetLayout setLayout)
    {
        unsafe
        {
            DescriptorPoolSize poolSize = new() { Type = DescriptorType.StorageBuffer, DescriptorCount = 15 };

            DescriptorPoolCreateInfo poolInfo = new()
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                MaxSets = 5,
                PoolSizeCount = 1,
                PPoolSizes = &poolSize
            };

            Check(vk.CreateDescriptorPool(device, &poolInfo, null, out DescriptorPool pool), "vkCreateDescriptorPool");

            DescriptorSetLayout* layouts = stackalloc DescriptorSetLayout[5];
            for (int i = 0; i < 5; i++)
            {
                layouts[i] = setLayout;
            }

            DescriptorSetAllocateInfo allocInfo = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = pool,
                DescriptorSetCount = 5,
                PSetLayouts = layouts
            };

            DescriptorSet* sets = stackalloc DescriptorSet[5];
            Check(vk.AllocateDescriptorSets(device, &allocInfo, sets), "vkAllocateDescriptorSets");
            return (pool, sets[0], sets[1], sets[2], sets[3], sets[4]);
        }
    }

    private static ShaderModule CreateShaderModule(Vk vk, Device device, string resourceSuffix)
    {
        unsafe
        {
            Assembly assembly = typeof(VulkanBc7Encoder).Assembly;
            string? resourceName = Array.Find(
                assembly.GetManifestResourceNames(),
                name => name.EndsWith(resourceSuffix, StringComparison.Ordinal));

            if (resourceName is null)
            {
                throw new InvalidOperationException(
                    $"Embedded SPIR-V resource ending with '{resourceSuffix}' was not found.");
            }

            using Stream stream = assembly.GetManifestResourceStream(resourceName)
                                  ?? throw new InvalidOperationException(
                                      $"Could not open embedded SPIR-V stream '{resourceName}'.");

            byte[] bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);

            fixed (byte* pCode = bytes)
            {
                ShaderModuleCreateInfo createInfo = new()
                {
                    SType = StructureType.ShaderModuleCreateInfo,
                    CodeSize = (nuint)bytes.Length,
                    PCode = (uint*)pCode
                };

                Check(vk.CreateShaderModule(device, &createInfo, null, out ShaderModule module),
                    "vkCreateShaderModule");
                return module;
            }
        }
    }

    private static Pipeline CreateComputePipeline(
        Vk vk,
        Device device,
        PipelineLayout layout,
        ShaderModule module,
        string entryPoint)
    {
        unsafe
        {
            byte[] entryBytes = Encoding.UTF8.GetBytes(entryPoint + '\0');
            fixed (byte* pEntry = entryBytes)
            {
                PipelineShaderStageCreateInfo stageInfo = new()
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.ComputeBit,
                    Module = module,
                    PName = pEntry
                };

                ComputePipelineCreateInfo pipelineInfo = new()
                {
                    SType = StructureType.ComputePipelineCreateInfo, Stage = stageInfo, Layout = layout
                };

                Pipeline pipeline;
                Check(vk.CreateComputePipelines(device, default, 1, &pipelineInfo, null, &pipeline),
                    "vkCreateComputePipelines");
                return pipeline;
            }
        }
    }

    private static void Check(Result result, string operation)
    {
        if (result != Result.Success)
        {
            throw new InvalidOperationException($"{operation} failed with Vulkan result {result}.");
        }
    }

    private void EnsureBufferCapacity(ulong pixelBytes, ulong blockCount)
    {
        if (pixelBytes <= this.stagingInCapacity && blockCount <= this.blockCapacity)
        {
            return;
        }

        this.FreeBuffers();

        ulong newPixelCapacity = Math.Max(pixelBytes, 1024UL * 1024UL * 4UL);
        ulong newBlockCapacity = Math.Max(blockCount, 1024UL / 4UL * (1024UL / 4UL));
        ulong blockBytes = newBlockCapacity * BlockBytes;

        this.CreateBuffer(
            newPixelCapacity,
            BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out this.stagingInBuffer,
            out this.stagingInMemory);
        this.stagingInMapped = this.MapBuffer(this.stagingInMemory, newPixelCapacity);

        this.CreateBuffer(
            newPixelCapacity,
            BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit,
            MemoryPropertyFlags.DeviceLocalBit,
            out this.deviceInBuffer,
            out this.deviceInMemory);

        this.CreateBuffer(
            blockBytes,
            BufferUsageFlags.StorageBufferBit,
            MemoryPropertyFlags.DeviceLocalBit,
            out this.err1Buffer,
            out this.err1Memory);

        this.CreateBuffer(
            blockBytes,
            BufferUsageFlags.StorageBufferBit,
            MemoryPropertyFlags.DeviceLocalBit,
            out this.err2Buffer,
            out this.err2Memory);

        this.CreateBuffer(
            blockBytes,
            BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.DeviceLocalBit,
            out this.deviceOutBuffer,
            out this.deviceOutMemory);

        this.CreateBuffer(
            blockBytes,
            BufferUsageFlags.TransferDstBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out this.stagingOutBuffer,
            out this.stagingOutMemory);
        this.stagingOutMapped = this.MapBuffer(this.stagingOutMemory, blockBytes);

        this.stagingInCapacity = newPixelCapacity;
        this.blockCapacity = newBlockCapacity;

        this.UpdateDescriptorSet(this.set456, this.deviceInBuffer, newPixelCapacity, this.err1Buffer, blockBytes,
            this.err1Buffer, blockBytes);
        this.UpdateDescriptorSet(this.set1To2, this.deviceInBuffer, newPixelCapacity, this.err1Buffer, blockBytes,
            this.err2Buffer, blockBytes);
        this.UpdateDescriptorSet(this.set2To1, this.deviceInBuffer, newPixelCapacity, this.err2Buffer, blockBytes,
            this.err1Buffer, blockBytes);
        this.UpdateDescriptorSet(this.setEncodeQuick, this.deviceInBuffer, newPixelCapacity, this.err1Buffer,
            blockBytes, this.deviceOutBuffer, blockBytes);
        this.UpdateDescriptorSet(this.setEncodeDefault, this.deviceInBuffer, newPixelCapacity, this.err2Buffer,
            blockBytes, this.deviceOutBuffer, blockBytes);
    }

    private void UpdateDescriptorSet(
        DescriptorSet set,
        Buffer b0,
        ulong size0,
        Buffer b1,
        ulong size1,
        Buffer b2,
        ulong size2)
    {
        unsafe
        {
            DescriptorBufferInfo* infos = stackalloc DescriptorBufferInfo[3];
            infos[0] = new DescriptorBufferInfo { Buffer = b0, Offset = 0, Range = size0 };
            infos[1] = new DescriptorBufferInfo { Buffer = b1, Offset = 0, Range = size1 };
            infos[2] = new DescriptorBufferInfo { Buffer = b2, Offset = 0, Range = size2 };

            WriteDescriptorSet* writes = stackalloc WriteDescriptorSet[3];
            for (uint i = 0; i < 3; i++)
            {
                writes[i] = new WriteDescriptorSet
                {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = set,
                    DstBinding = i,
                    DescriptorCount = 1,
                    DescriptorType = DescriptorType.StorageBuffer,
                    PBufferInfo = &infos[i]
                };
            }

            this.vk.UpdateDescriptorSets(this.device, 3, writes, 0, null);
        }
    }

    private void RecordAndSubmit(
        uint width,
        uint height,
        uint blocksX,
        uint totalBlocks,
        ulong pixelBytes,
        ulong outputBytes,
        Bc7EncodingQuality quality)
    {
        unsafe
        {
            Check(this.vk.ResetCommandBuffer(this.commandBuffer, 0), "vkResetCommandBuffer");

            CommandBufferBeginInfo beginInfo = new()
            {
                SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit
            };

            Check(this.vk.BeginCommandBuffer(this.commandBuffer, &beginInfo), "vkBeginCommandBuffer");

            BufferCopy copyIn = new() { SrcOffset = 0, DstOffset = 0, Size = pixelBytes };
            this.vk.CmdCopyBuffer(this.commandBuffer, this.stagingInBuffer, this.deviceInBuffer, 1, &copyIn);

            this.InsertMemoryBarrier(
                AccessFlags.TransferWriteBit,
                AccessFlags.ShaderReadBit,
                PipelineStageFlags.TransferBit,
                PipelineStageFlags.ComputeShaderBit);

            uint groupsQuarter = Math.Max(1u, (totalBlocks + 3u) / 4u);

            PushConstants pc = new()
            {
                TexWidth = width,
                NumBlockX = blocksX,
                Format = 98u,
                ModeId = 0u,
                StartBlockId = 0u,
                NumTotalBlocks = totalBlocks,
                AlphaWeight = 1.0f,
                TexHeight = height
            };

            // Pass 1: TryMode456CS (4 blocks per 64-thread workgroup) -> writes err1
            this.vk.CmdBindPipeline(this.commandBuffer, PipelineBindPoint.Compute, this.pipeline456);
            DescriptorSet set = this.set456;
            this.vk.CmdBindDescriptorSets(this.commandBuffer, PipelineBindPoint.Compute, this.pipelineLayout, 0, 1,
                &set, 0, null);
            this.vk.CmdPushConstants(this.commandBuffer, this.pipelineLayout, ShaderStageFlags.ComputeBit, 0,
                (uint)Unsafe.SizeOf<PushConstants>(), &pc);
            this.vk.CmdDispatch(this.commandBuffer, groupsQuarter, 1, 1);

            bool run137 = quality != Bc7EncodingQuality.Quick;
            if (run137)
            {
                // Pass 2: TryMode137CS for modes 1, 3, 7 (1 block per 64-thread workgroup)
                // Mode 1: err1 -> err2 (set1To2)
                // Mode 3: err2 -> err1 (set2To1)
                // Mode 7: err1 -> err2 (set1To2)
                this.vk.CmdBindPipeline(this.commandBuffer, PipelineBindPoint.Compute, this.pipeline137);

                for (int i = 0; i < Mode137Order.Length; i++)
                {
                    this.InsertMemoryBarrier(
                        AccessFlags.ShaderWriteBit,
                        AccessFlags.ShaderReadBit,
                        PipelineStageFlags.ComputeShaderBit,
                        PipelineStageFlags.ComputeShaderBit);

                    pc.ModeId = Mode137Order[i];
                    set = (i & 1) == 0 ? this.set1To2 : this.set2To1;
                    this.vk.CmdBindDescriptorSets(this.commandBuffer, PipelineBindPoint.Compute, this.pipelineLayout, 0,
                        1, &set, 0, null);
                    this.vk.CmdPushConstants(this.commandBuffer, this.pipelineLayout, ShaderStageFlags.ComputeBit, 0,
                        (uint)Unsafe.SizeOf<PushConstants>(), &pc);
                    this.vk.CmdDispatch(this.commandBuffer, totalBlocks, 1, 1);
                }
            }

            // Pass 3: EncodeBlockCS (4 blocks per 64-thread workgroup) -> writes deviceOutBuffer
            this.InsertMemoryBarrier(
                AccessFlags.ShaderWriteBit,
                AccessFlags.ShaderReadBit,
                PipelineStageFlags.ComputeShaderBit,
                PipelineStageFlags.ComputeShaderBit);

            this.vk.CmdBindPipeline(this.commandBuffer, PipelineBindPoint.Compute, this.pipelineEncode);
            set = run137 ? this.setEncodeDefault : this.setEncodeQuick;
            this.vk.CmdBindDescriptorSets(this.commandBuffer, PipelineBindPoint.Compute, this.pipelineLayout, 0, 1,
                &set, 0, null);
            this.vk.CmdPushConstants(this.commandBuffer, this.pipelineLayout, ShaderStageFlags.ComputeBit, 0,
                (uint)Unsafe.SizeOf<PushConstants>(), &pc);
            this.vk.CmdDispatch(this.commandBuffer, groupsQuarter, 1, 1);

            this.InsertMemoryBarrier(
                AccessFlags.ShaderWriteBit,
                AccessFlags.TransferReadBit,
                PipelineStageFlags.ComputeShaderBit,
                PipelineStageFlags.TransferBit);

            BufferCopy copyOut = new() { SrcOffset = 0, DstOffset = 0, Size = outputBytes };
            this.vk.CmdCopyBuffer(this.commandBuffer, this.deviceOutBuffer, this.stagingOutBuffer, 1, &copyOut);

            Check(this.vk.EndCommandBuffer(this.commandBuffer), "vkEndCommandBuffer");

            Fence submitFence = this.fence;
            Check(this.vk.ResetFences(this.device, 1, &submitFence), "vkResetFences");

            CommandBuffer cmd = this.commandBuffer;
            SubmitInfo submitInfo = new()
            {
                SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &cmd
            };

            Check(this.vk.QueueSubmit(this.queue, 1, &submitInfo, submitFence), "vkQueueSubmit");
            Check(this.vk.WaitForFences(this.device, 1, &submitFence, Vk.True, ulong.MaxValue), "vkWaitForFences");
        }
    }

    private void InsertMemoryBarrier(
        AccessFlags srcAccess,
        AccessFlags dstAccess,
        PipelineStageFlags srcStage,
        PipelineStageFlags dstStage)
    {
        unsafe
        {
            MemoryBarrier barrier = new()
            {
                SType = StructureType.MemoryBarrier, SrcAccessMask = srcAccess, DstAccessMask = dstAccess
            };

            this.vk.CmdPipelineBarrier(
                this.commandBuffer,
                srcStage,
                dstStage,
                0,
                1,
                &barrier,
                0,
                null,
                0,
                null);
        }
    }

    private void CreateBuffer(
        ulong size,
        BufferUsageFlags usage,
        MemoryPropertyFlags properties,
        out Buffer buffer,
        out DeviceMemory memory)
    {
        unsafe
        {
            BufferCreateInfo bufferInfo = new()
            {
                SType = StructureType.BufferCreateInfo,
                Size = size,
                Usage = usage,
                SharingMode = SharingMode.Exclusive
            };

            Check(this.vk.CreateBuffer(this.device, &bufferInfo, null, out buffer), "vkCreateBuffer");

            this.vk.GetBufferMemoryRequirements(this.device, buffer, out MemoryRequirements memReqs);
            uint memoryTypeIndex = this.FindMemoryType(memReqs.MemoryTypeBits, properties);

            MemoryAllocateInfo allocInfo = new()
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memReqs.Size,
                MemoryTypeIndex = memoryTypeIndex
            };

            Check(this.vk.AllocateMemory(this.device, &allocInfo, null, out memory), "vkAllocateMemory");
            Check(this.vk.BindBufferMemory(this.device, buffer, memory, 0), "vkBindBufferMemory");
        }
    }

    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        this.vk.GetPhysicalDeviceMemoryProperties(this.physicalDevice, out PhysicalDeviceMemoryProperties memProps);

        for (uint i = 0; i < memProps.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1u << (int)i)) != 0
                && (memProps.MemoryTypes[(int)i].PropertyFlags & properties) == properties)
            {
                return i;
            }
        }

        // Fallback if DeviceLocal is not separate from HostVisible on unified architectures
        for (uint i = 0; i < memProps.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1u << (int)i)) != 0)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"Could not find suitable Vulkan memory type for {properties}.");
    }

    private nint MapBuffer(DeviceMemory memory, ulong size)
    {
        unsafe
        {
            void* mapped = null;
            Check(this.vk.MapMemory(this.device, memory, 0, size, 0, &mapped), "vkMapMemory");
            return (nint)mapped;
        }
    }

    private void FreeBuffers()
    {
        if (this.stagingInMapped != 0)
        {
            this.vk.UnmapMemory(this.device, this.stagingInMemory);
            this.stagingInMapped = 0;
        }

        if (this.stagingOutMapped != 0)
        {
            this.vk.UnmapMemory(this.device, this.stagingOutMemory);
            this.stagingOutMapped = 0;
        }

        DestroyBufferPair(this.vk, this.device, ref this.stagingInBuffer, ref this.stagingInMemory);
        DestroyBufferPair(this.vk, this.device, ref this.deviceInBuffer, ref this.deviceInMemory);
        DestroyBufferPair(this.vk, this.device, ref this.err1Buffer, ref this.err1Memory);
        DestroyBufferPair(this.vk, this.device, ref this.err2Buffer, ref this.err2Memory);
        DestroyBufferPair(this.vk, this.device, ref this.deviceOutBuffer, ref this.deviceOutMemory);
        DestroyBufferPair(this.vk, this.device, ref this.stagingOutBuffer, ref this.stagingOutMemory);

        this.stagingInCapacity = 0;
        this.blockCapacity = 0;
    }

    private static void DestroyBufferPair(Vk vk, Device device, ref Buffer buffer, ref DeviceMemory memory)
    {
        unsafe
        {
            if (buffer.Handle != 0)
            {
                vk.DestroyBuffer(device, buffer, null);
                buffer = default;
            }

            if (memory.Handle != 0)
            {
                vk.FreeMemory(device, memory, null);
                memory = default;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PushConstants
    {
        public uint TexWidth;
        public uint NumBlockX;
        public uint Format;
        public uint ModeId;
        public uint StartBlockId;
        public uint NumTotalBlocks;
        public float AlphaWeight;
        public uint TexHeight;
    }
}
