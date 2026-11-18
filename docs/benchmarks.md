# Benchmarks & DirectXTex Parity

## Running the Benchmarks

```sh
# Build the in-process DirectXTex reference shim (links the same libDirectXTex.a as texconv)
./scripts/build-texconv.sh
./scripts/build-dxtex-shim.sh

# Run paired in-process encode benchmarks (Ours vs. DirectXTex)
dotnet run -c Release --project tests/ImageSharp.Textures.Benchmarks -f net10.0 -- --filter '*Paired*' --job Short

# Run decode, encode, and container I/O benchmarks
dotnet run -c Release --project tests/ImageSharp.Textures.Benchmarks -f net10.0 -- \
    --filter '*BcDecodeBenchmarks*' '*BcEncodeBenchmarks*' '*Bc7EncodeBenchmarks*' '*TextureIoBenchmarks*' --job Short
```

Environment for the recorded results below:

| | |
|---|---|
| **CPU** | AMD Ryzen 7 5800X (8 cores / 16 threads, AVX2) |
| **OS** | Linux 7.3.1-1-cachyos (x64) |
| **Runtime** | .NET 10.0.14 (RyuJIT x86-64-v3) |
| **Harness** | BenchmarkDotNet 0.15.8 (`--job Short`: 1 launch, 3 warmup, 3 measured iterations) |
| **Reference Oracle** | `Texconv-Custom-DLL` `40b28a0` / `DirectXTex` `232bbf2e` (`-O3 -DNDEBUG`, single-threaded CPU codec) |

---

## 1. Decode Throughput

Single surface decoded to `Rgba32` (`BcDecodeBenchmarks`):

| Format | Surface Size | `TexelOperations.Decode<Rgba32>` | `BlockDecode` (raw RGBA32) | Throughput | DirectXTex (in-process) | Ours / DirectXTex |
|---|---|---:|---:|---:|---:|---:|
| `Bc1_UNorm` | 256² | 176.0 µs | 159.8 µs | 410 MP/s | 243.0 µs | **0.66×** |
| `Bc1_UNorm` | 1024² | 2.77 ms | 2.61 ms | 378 MP/s | 4.19 ms | **0.62×** |
| `Bc1_UNorm` | 4096² | 50.63 ms | 44.26 ms | 379 MP/s | 65.73 ms | **0.67×** |
| `Bc4_UNorm` | 256² | 160.1 µs | 151.7 µs | 432 MP/s | 218.0 µs | **0.70×** |
| `Bc4_UNorm` | 1024² | 2.59 ms | 2.47 ms | 380 MP/s | 3.55 ms | **0.70×** |
| `Bc4_UNorm` | 4096² | 45.01 ms | 37.71 ms | 445 MP/s | 59.68 ms | **0.63×** |
| `Bc5_UNorm` | 256² | 195.0 µs | 175.3 µs | 374 MP/s | 311.0 µs | **0.56×** |
| `Bc5_UNorm` | 1024² | 3.02 ms | 2.95 ms | 348 MP/s | 4.94 ms | **0.60×** |
| `Bc5_UNorm` | 4096² | 51.85 ms | 47.16 ms | 356 MP/s | 76.09 ms | **0.62×** |
| `Bc7_UNorm` | 256² | 663.3 µs | 663.0 µs | 98.8 MP/s | 669.0 µs | **0.99×** |
| `Bc7_UNorm` | 1024² | 10.53 ms | 10.78 ms | 99.6 MP/s | 11.09 ms | **0.97×** |
| `Bc7_UNorm` | 4096² | 179.65 ms | 170.49 ms | 98.4 MP/s | 172.70 ms | **0.99×** |

`Ours / DirectXTex` below `1.00×` means `ImageSharp.Textures` is faster than C++ DirectXTex.

---

## 2. Encode Throughput

### Paired In-Process Encode (`PairedBcEncodeBenchmarks`, 1024×1024)

Both arms run in the same process on the same packed RGBA32 buffer; `[GlobalSetup]` verifies that our output payload is **byte-for-byte identical** to DirectXTex before timing:

| Format | Ours (Safe C#) | DirectXTex (C++) | Ratio (Ours / DirectXTex) | Allocated |
|---|---:|---:|---:|---:|
| `Bc1_UNorm`, 1024² | 8.70 ms | 7.97 ms | **1.09×** | 0 B |
| `Bc4_UNorm`, 1024² | 5.44 ms | 7.75 ms | **0.70×** (1.42× faster) | 0 B |
| `Bc5_UNorm`, 1024² | 11.60 ms | 13.67 ms | **0.85×** (1.18× faster) | 0 B |

### Full Encode Matrix (`BcEncodeBenchmarks` & `Bc7EncodeBenchmarks`)

| Format | Quality | 256² (`Encode`) | 1024² (`Encode`) | 1024² (`EncodePacked`) | 1024² DirectXTex | 1024² Ratio | Allocated (1024²) |
|---|---|---:|---:|---:|---:|---:|---:|
| `Bc1_UNorm` | — | 910.7 µs | 9.46 ms | 9.25 ms | 8.34 ms | 1.11× | 96 B |
| `Bc4_UNorm` | — | 496.5 µs | 6.64 ms | 5.44 ms | 8.07 ms | **0.67×** | 96 B |
| `Bc5_UNorm` | — | 891.7 µs | 12.44 ms | 11.60 ms | 13.75 ms | **0.84×** | 96 B |
| `Bc7_UNorm` | `Quick` | 120.2 ms | 2.31 s | — | 4.00 s | **0.58×** | 96 B |
| `Bc7_UNorm` | `Default` | 4.17 s | 53.16 s | — | 107.54 s | **0.49×** | 960 B |

### BC7 Multi-Threaded CPU & GPU Compute Comparison (`Bc7BackendComparisonBenchmarks`)

Measured on AMD Ryzen 7 5800X (8 cores / 16 threads) + Intel Arc A770 Graphics (DG2, 16 GiB, Vulkan 1.4.346 Mesa 26.0.4 + OpenCL 3.0 NEO):

| Backend | Implementation | `Quick` (64²) | `Quick` (256²) | `Quick` (1024²) | `Default` (64²) | `Default` (256²) | `Default` (1024²) | Speedup vs. ST CPU (1024² `Default`) |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| **ST CPU** | `Bc7Encoder.Encode` | 21.77 ms | 108.83 ms | 2.20 s | 357.73 ms | 4.13 s | 54.31 s | 1.00× |
| **MT CPU (16T)** | `Bc7Encoder.EncodeParallel` | 3.53 ms | 15.20 ms | 277.30 ms | 46.73 ms | 447.80 ms | 6.29 s | **8.64×** |
| **Vulkan GPU** | `VulkanBc7Encoder` (`Silk.NET.Vulkan` + SPIR-V `BC7Encode.hlsl`) | 307.1 µs | 491.0 µs | 3.56 ms | 594.1 µs | 3.32 ms | 20.58 ms | **2,640×** |
| **ILGPU GPU** | `IlgpuBc7Encoder` (OpenCL on Arc A770) | 611.3 µs | 682.2 µs | 2.06 ms | 879.9 µs | 2.33 ms | 18.02 ms | **3,013×** |
| **ILGPU CPU** | `IlgpuBc7Encoder(preferCpu: true)` (CPU workgroup emulator) | 2.11 s | — | — | 8.33 s | — | — | 0.043× (at 64²) |

- `Bc7Encoder.EncodeParallel` is **bit-for-bit identical** to `Bc7Encoder.Encode` and `texconv` CPU BC7 across all block modes and dimensions.
- **`ImageSharp.Textures.ILGPU` (`IlgpuBc7Encoder`) is the recommended and published NuGet package for GPU-accelerated BC7 encoding**: it is written in 100% safe C# (`<AllowUnsafeBlocks>false</AllowUnsafeBlocks>`), runs across CUDA and OpenCL GPUs without external shader toolchains, and outperforms the raw Vulkan SPIR-V pipeline at `256²` and `1024²` (**18.02 ms** vs. **20.58 ms** at `1024² Default`, **3,013×** faster than ST CPU; **2.06 ms** vs. **3.56 ms** at `1024² Quick`, **1,064×** faster than ST CPU).
- **Avoid ILGPU's CPU accelerator in production**: ILGPU's CPU backend emulates 64-thread GPU workgroups with barrier synchronization on the CPU (`2.11 s` at `64² Quick`, `8.33 s` at `64² Default`). `IlgpuBc7Encoder` therefore restricts its public constructor and `IlgpuBc7Encoder.TryCreate` to hardware GPU accelerators (`Cuda` and `OpenCL`), falling back to `Bc7Encoder.EncodeParallel` on the CPU when no GPU is present.
- **`ImageSharp.Textures.Vulkan` (`VulkanBc7Encoder`)** executes the same DirectXTex `BC7Encode.hlsl` 3-pass compute pipeline via `Silk.NET.Vulkan` and precompiled SPIR-V shaders, producing **100% byte-for-byte identical** BC7 output to `IlgpuBc7Encoder` across all tested surfaces (including partial edge blocks). It is retained in the repository as an unpacked reference implementation (`<IsPackable>false</IsPackable>`) for native SPIR-V and NativeAOT scenarios.

---

## 3. Container I/O & Surface Interop (`TextureIoBenchmarks`)

Measured on a 1024×1024 `Bc7_UNorm` DDS texture with 4 mip levels:

| Benchmark | Description | Mean | Allocated |
|---|---|---:|---:|
| `Load` | Parse DDS headers and load all 4 mip levels into pooled surface buffers | 27.76 µs | 1,992 B |
| `Save` | Write the 4-mip BC7 texture to a stream (payloads copied without recompression) | 25.68 µs | 160 B |
| `ToImage` | Decode the 1024×1024 BC7 base level to `Image<Rgba32>` | 11.11 ms | 1,096 B |
| `CopyFrom` | Re-encode a 1024×1024 `Image<Rgba32>` into the BC7 base level (`Default` quality) | 53.16 s | 960 B |

---

## 4. Parity Verification Against `texconv`

`tests/ImageSharp.Textures.Parity.Tests` validates decode, encode, and container interoperability against the pinned `texconv` binary across a 9-image synthetic corpus (gradients, checkerboard, seeded noise, alpha gradient, normal map, and non-multiple-of-4 dimensions `17×33` and `1×1`):

- **Decode parity (`DecodeParityTests`):**
  - `BC7_UNORM`: **0 LSB** max difference (bit-exact across all pixels and images).
  - `BC1_UNORM`, `BC4_UNORM`, `BC5_UNORM`: **≤ 1 LSB** max difference (exact on endpoints; 1 LSB where `texconv` interpolates in float before quantizing).
- **Encode parity (`EncodeParityTests`):**
  - Default DDS output is **byte-identical** (both header and compressed block payload) to `texconv` across all 36 `(format, image)` pairs for BC1, BC4, BC5, and BC7.
- **Cross-container identity (`CrossContainerTests` & `ContainerInteropTests`):**
  - Flat textures, mip chains, texture arrays, cube maps, cube map arrays, and 3D volume textures preserve byte-identical surface payloads and surface ordering across `DDS → KTX2 → KTX v1 → DDS` round trips, and every DDS written by `ImageSharp.Textures` is accepted by `texconv`.
