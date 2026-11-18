# Contributing to ImageSharp.Textures

## Reporting Bugs

- Search existing [Issues](https://github.com/zznty/ImageSharp.Textures/issues) before opening a new one.
- Include the target framework, OS, container/texel format, and a minimal reproduction or test case.

## Pull Requests

1. Clone the repository and build the solution:
   ```sh
   dotnet build ImageSharp.Textures.sln -c Release
   dotnet test tests/ImageSharp.Textures.Tests -c Release
   ```
2. For changes touching texel codecs or container headers, build the pinned `texconv` oracle and run the parity suite:
   ```sh
   ./scripts/build-texconv.sh
   TEXCONV_PATH="$PWD/artifacts/texconv/texconv" dotnet test tests/ImageSharp.Textures.Parity.Tests -c Release
   ```
3. For changes touching SIMD paths (`Bc4Encoder`, `Bc7Encoder`), verify all unit tests pass under hardware-intrinsic toggles:
   ```sh
   dotnet test tests/ImageSharp.Textures.Tests -c Release
   DOTNET_EnableAVX2=0 dotnet test tests/ImageSharp.Textures.Tests -c Release --no-build
   DOTNET_EnableHWIntrinsic=0 dotnet test tests/ImageSharp.Textures.Tests -c Release --no-build
   ```
4. Keep `src/ImageSharp.Textures` 100% safe C# (`<AllowUnsafeBlocks>false</AllowUnsafeBlocks>`, Unsafe v2 enabled) with zero compiler or analyzer warnings in `Release`.
