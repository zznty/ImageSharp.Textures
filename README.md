# ImageSharp.Textures

[![Build Status](https://img.shields.io/github/actions/workflow/status/zznty/ImageSharp.Textures/build-and-test.yml?branch=main)](https://github.com/zznty/ImageSharp.Textures/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

`ImageSharp.Textures` is a managed C# texture container and block-compression library built on top of `SixLabors.ImageSharp` v4.

Instead of collapsing a texture container into a single raster image, `ImageSharp.Textures` models the full resource hierarchy: a `Texture` owns ordered mip levels (`TextureMipLevelCollection`), each mip level owns ordered surfaces (`TextureSurfaceCollection`: array layers, cube faces, or volume depth slices), and every `TextureSurface` owns its encoded payload. Surfaces can be materialized to `Image<TPixel>`, edited with ImageSharp, and re-encoded back into the surface; saving a texture writes the current payload of every surface without recompressing untouched surfaces.

## Features

- **100% safe C#** (`<AllowUnsafeBlocks>false</AllowUnsafeBlocks>`, C# Unsafe v2 `MemorySafetyRules` v2 enabled) with SIMD acceleration (`Vector<T>`, `Vector256<T>`, `Vector128<T>`) and scalar fallbacks.
- **Bit-exact parity with DirectXTex / `texconv`**: default DDS encodes are byte-identical (header and payload) to `texconv` for BC1, BC4, BC5, and BC7, and surface payloads round-trip byte-for-byte across DDS ↔ KTX v1 ↔ KTX2.
- **Bounded streaming memory**: texel codecs process one row (uncompressed) or one 4-row block strip (BC1/BC4/BC5/BC7 decode and BC1/BC4/BC5 encode) at a time without allocating full intermediate RGBA images.

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
| **Encode time (1024²)** | 8.70 ms | 5.44 ms | 11.60 ms | 2.31 s | 53.16 s |
| **Encode vs. C++ DirectXTex** | **1.09×** (within 9%) | **1.30× faster** | **1.18× faster** | **1.73× faster** | **2.02× faster** |

## License

Licensed under the [MIT License](LICENSE).
