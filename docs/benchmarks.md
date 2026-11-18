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
