# ImageSharp.Textures

[![Build Status](https://img.shields.io/github/actions/workflow/status/zznty/ImageSharp.Textures/build-and-test.yml?branch=main)](https://github.com/zznty/ImageSharp.Textures/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

`ImageSharp.Textures` is a managed C# texture container and block-compression library built on top of `SixLabors.ImageSharp` v4.

Instead of collapsing a texture container into a single raster image, `ImageSharp.Textures` models the full resource hierarchy: a `Texture` owns ordered mip levels (`TextureMipLevelCollection`), each mip level owns ordered surfaces (`TextureSurfaceCollection`: array layers, cube faces, or volume depth slices), and every `TextureSurface` owns its encoded payload. Surfaces can be materialized to `Image<TPixel>`, edited with ImageSharp, and re-encoded back into the surface; saving a texture writes the current payload of every surface without recompressing untouched surfaces.

## Features

- **100% safe C#** (`<AllowUnsafeBlocks>false</AllowUnsafeBlocks>`, C# Unsafe v2 `MemorySafetyRules` v2 enabled) with SIMD acceleration (`Vector<T>`, `Vector256<T>`, `Vector128<T>`) and scalar fallbacks.
- **Bit-exact parity with DirectXTex / `texconv`**: default CPU DDS encodes are byte-identical (header and payload) to `texconv` for BC1, BC4, BC5, and BC7 (both single-threaded and multi-threaded `MaxDegreeOfParallelism`), and surface payloads round-trip byte-for-byte across DDS ↔ KTX v1 ↔ KTX2.
- **Portable GPU BC7 compute (`ImageSharp.Textures.ILGPU`)**: 100% safe C# port of the DirectXTex `BC7Encode.hlsl` 3-pass compute pipeline running on CUDA and OpenCL GPUs via ILGPU (**18.02 ms** for a 1024×1024 `Default` BC7 encode on Intel Arc A770 — **3,013×** faster than single-threaded CPU).
- **Bounded streaming memory**: texel codecs process one row (uncompressed) or one 4-row block strip (BC1/BC4/BC5/BC7 decode and BC1/BC4/BC5 encode) at a time without allocating full intermediate RGBA images.

### Packages

| Package | NuGet | Description |
|---|---|---|
| `ImageSharp.Textures` | Published | Core texture containers (DDS, KTX v1, KTX2), surface hierarchy, SIMD BC1/BC4/BC5/BC7 codecs, multi-threaded CPU BC7 encoder, and `IBc7HardwareEncoder` abstraction. |
| `ImageSharp.Textures.ILGPU` | Published | Recommended 100% safe C# GPU BC7 encoder (`IlgpuBc7Encoder`) targeting CUDA and OpenCL GPUs via [ILGPU](https://github.com/m4rs-mt/ILGPU). |
| `ImageSharp.Textures.Vulkan` | Source-only (`IsPackable=false`) | Reference native Vulkan Compute + precompiled SPIR-V (`BC7Encode.hlsl`) BC7 encoder (`VulkanBc7Encoder`) via `Silk.NET.Vulkan` for native SPIR-V / NativeAOT scenarios. |

### Supported Containers (Read & Write)

| Container | Mip chains | Texture arrays | Cube maps & cube arrays | Volume (3D) | Key/value metadata |
|---|---|---|---|---|---|
| [DDS](https://learn.microsoft.com/en-us/windows/win32/direct3ddds/dx-graphics-dds) (legacy FourCC + `DDS_HEADER_DXT10`) | Yes | Yes (`DX10`) | Yes | Yes | N/A |
| [KTX v1](https://registry.khronos.org/KTX/specs/1.0/ktxspec.v1.html) | Yes | Yes | Yes | Yes | Yes (byte-exact) |
| [KTX2](https://registry.khronos.org/KTX/specs/2.0/ktxspec.v2.html) (uncompressed / scheme 0) | Yes | Yes | Yes | Yes | Yes (byte-exact) |

### Supported Texel Formats (Decode & Encode)

| `TexelFormat` | Block / Texel Size | DDS (`DXGI_FORMAT` / FourCC) | KTX v1 (`glInternalFormat`) | KTX2 (`VkFormat`) | Notes |
|---|---|---|---|---|---|
| `R8G8B8A8_UNorm` | 4 B / texel | `28` (`DX10`) | `0x8058` (`GL_RGBA8`) | `37` | Uncompressed 8-bit RGBA |
| `Bc1_UNorm` | 8 B / 4×4 block | `71` / `DXT1` | `0x83F1` | `133` | 4-color & 3-color 1-bit punch-through alpha |
| `Bc1_UnormSrgb` | 8 B / 4×4 block | `72` (`DX10`) | `0x8C4D` | `134` | sRGB BC1 |
| `Bc4_UNorm` | 8 B / 4×4 block | `80` / `BC4U` | `0x8DBB` | `139` | Single-channel red |
| `Bc5_UNorm` | 16 B / 4×4 block | `83` / `BC5U` | `0x8DBD` | `141` | Dual-channel red/green |
| `Bc7_UNorm` | 16 B / 4×4 block | `98` (`DX10`) | `0x8E8C` | `145` | Full mode search (`Default`) and mode-6 (`Quick`) |
| `Bc7_UnormSrgb` | 16 B / 4×4 block | `99` (`DX10`) | `0x8E8D` | `146` | sRGB BC7 |

sRGB is carried in container metadata (`dxgiFormat`, `glInternalFormat`, `vkFormat`, and the KTX2 Data Format Descriptor transfer function); texel operations preserve encoded byte values without color-space conversion.

## Usage

```csharp
using ImageSharp.Textures;
using ImageSharp.Textures.Formats.Dds;
using ImageSharp.Textures.Formats.Ktx2;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

// Load a texture by file extension or stream header detection.
using Texture texture = Texture.Load("albedo.dds");

// Inspect mip levels and surfaces (ordered by array layer -> cube face -> depth slice).
TextureMipLevel baseLevel = texture.MipLevels.BaseLevel;
TextureSurface surface = baseLevel.Surfaces[0];

// Materialize a detached ImageSharp image, edit it, and re-encode back into the surface.
using (Image<Rgba32> image = surface.ToImage<Rgba32>())
{
    image.Mutate(x => x.Grayscale());
    surface.CopyFrom(image);
}

// Save to another container or transcode to a target texel format on write.
texture.Save(
    "albedo.ktx2",
    new Ktx2TextureEncoder(new Ktx2EncodingOptions
    {
        TargetTexelFormat = TexelFormat.Bc7_UNorm,
        Bc7Quality = Bc7EncodingQuality.Quick,
    }));
```

### Multi-Threaded CPU & GPU-Accelerated BC7 Encoding

`DdsEncodingOptions`, `KtxEncodingOptions`, `Ktx2EncodingOptions`, and `BcEncodeOptions` can parallelize BC7 encoding across CPU cores via `MaxDegreeOfParallelism`, or offload BC7 compression to a CUDA / OpenCL GPU via the `ImageSharp.Textures.ILGPU` package (`IlgpuBc7Encoder`):

```csharp
using ImageSharp.Textures.Formats.Dds;
using ImageSharp.Textures.ILGPU;

// TryCreate selects a CUDA or OpenCL GPU accelerator (and returns false if no GPU is available).
IlgpuBc7Encoder.TryCreate(out IlgpuBc7Encoder? gpuEncoder);
using (gpuEncoder)
{
    texture.Save(
        "albedo.dds",
        new DdsTextureEncoder(new DdsEncodingOptions
        {
            TargetTexelFormat = TexelFormat.Bc7_UNorm,
            Bc7Quality = Bc7EncodingQuality.Default,
            Bc7HardwareEncoder = gpuEncoder,
            MaxDegreeOfParallelism = Environment.ProcessorCount, // CPU fallback when gpuEncoder is null
        }));
}
```

> **Note:** `ImageSharp.Textures.ILGPU` is published as a 100% managed C# NuGet package targeting CUDA and OpenCL GPUs (ILGPU's CPU workgroup emulator is intentionally excluded in favor of `MaxDegreeOfParallelism`). An alternative native Vulkan SPIR-V implementation (`ImageSharp.Textures.Vulkan`) is kept in the repository tree for reference and native-SPIR-V/AOT use cases, and is not published to NuGet.

### Explicit Decoder / Stream Overloads

```csharp
using FileStream input = File.OpenRead("cubemap.dds");
using Texture texture = Texture.Load(input, new DdsTextureDecoder());

using FileStream output = File.Create("cubemap.ktx2");
texture.Save(output, Ktx2Format.Instance);
```

## Building & Testing

Requirements: .NET 11 SDK

```sh
# Build and run unit tests
dotnet build -c Release
dotnet test tests/ImageSharp.Textures.Tests -c Release --no-build

# Build pinned texconv and run DirectXTex parity tests
./scripts/build-texconv.sh
TEXCONV_PATH="$PWD/artifacts/texconv/texconv" dotnet test tests/ImageSharp.Textures.Parity.Tests -c Release
```

## Performance & Benchmarks

See [docs/benchmarks.md](docs/benchmarks.md) for the full BenchmarkDotNet suite, DirectXTex in-process and CLI comparisons, and parity methodology.

Summary at 1024×1024 on AMD Ryzen 7 5800X (.NET 10.0.14, RyuJIT AVX2, 100% safe C#):

| Operation | `BC1_UNorm` | `BC4_UNorm` | `BC5_UNorm` | `BC7_UNorm` (`Quick`) | `BC7_UNorm` (`Default`) |
|---|---:|---:|---:|---:|---:|
| **Decode throughput** | 378 MP/s (2.61 ms) | 380 MP/s (2.47 ms) | 348 MP/s (2.95 ms) | 99.6 MP/s (10.53 ms) | 99.6 MP/s (10.53 ms) |
| **Decode vs. C++ DirectXTex** | **1.60× faster** | **1.43× faster** | **1.67× faster** | **1.03× faster** | **1.03× faster** |
| **Encode time (1024², ST CPU)** | 8.70 ms | 5.44 ms | 11.60 ms | 2.20 s | 54.31 s |
| **Encode vs. C++ DirectXTex (ST CPU)** | **1.09×** (within 9%) | **1.30× faster** | **1.18× faster** | **1.73× faster** | **2.02× faster** |

### BC7 Multi-Threaded CPU & GPU Compute Throughput

Measured on AMD Ryzen 7 5800X (8 cores / 16 threads) + Intel Arc A770 Graphics (16 GiB, OpenCL 3.0 NEO / Vulkan 1.4.346 Mesa 26.0.4):

| Backend | Quality | 64×64 | 256×256 | 1024×1024 | 1024² Speedup vs. ST CPU | 1024² Throughput |
|---|---|---:|---:|---:|---:|---:|
| **ST CPU (`Bc7Encoder.Encode`)** | `Quick` | 21.77 ms | 108.83 ms | 2.20 s | 1.00× | 0.48 MP/s |
| **MT CPU (`Bc7Encoder.EncodeParallel`, 16T)** | `Quick` | 3.53 ms | 15.20 ms | 277.30 ms | **7.92×** | 3.78 MP/s |
| **ILGPU GPU (`IlgpuBc7Encoder`, OpenCL/CUDA)** | `Quick` | 611.3 µs | 682.2 µs | **2.06 ms** | **1,064×** | **508.1 MP/s** |
| **Vulkan GPU (`VulkanBc7Encoder`, SPIR-V ref)** | `Quick` | 307.1 µs | 491.0 µs | 3.56 ms | **617×** | 294.6 MP/s |
| **ST CPU (`Bc7Encoder.Encode`)** | `Default` | 357.73 ms | 4.13 s | 54.31 s | 1.00× | 0.019 MP/s |
| **MT CPU (`Bc7Encoder.EncodeParallel`, 16T)** | `Default` | 46.73 ms | 447.80 ms | 6.29 s | **8.64×** | 0.167 MP/s |
| **ILGPU GPU (`IlgpuBc7Encoder`, OpenCL/CUDA)** | `Default` | 879.9 µs | 2.33 ms | **18.02 ms** | **3,013×** | **58.2 MP/s** |
| **Vulkan GPU (`VulkanBc7Encoder`, SPIR-V ref)** | `Default` | 594.1 µs | 3.32 ms | 20.58 ms | **2,640×** | 51.0 MP/s |

- `Bc7Encoder.EncodeParallel` is **bit-for-bit identical** to single-threaded `Bc7Encoder.Encode` and `texconv` CPU BC7.
- `IlgpuBc7Encoder` and `VulkanBc7Encoder` produce **100% byte-for-byte identical** BC7 blocks to each other across all tested surfaces (including partial edge blocks via DirectXTex `{ 0, 0, 0, 1 }` edge replication).

## License

Licensed under the [MIT License](LICENSE).
