using System.Buffers;
using System.Buffers.Binary;
using ImageSharp.Textures.Formats.Dds.Enums;
using ImageSharp.Textures.Texels.BcCommon;

namespace ImageSharp.Textures.Formats.Dds;

/// <summary>
///     Writes the DDS header and the current surface payloads of a texture.
/// </summary>
internal sealed class DdsEncoderCore
{
    private readonly DdsEncodingOptions options;

    internal DdsEncoderCore(DdsEncodingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options;
    }

    /// <summary>
    ///     Encodes the texture to the given stream.
    /// </summary>
    /// <param name="texture">The texture to encode.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    internal void Encode(Texture texture, Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(stream);

        DdsEncodingPlan plan = DdsEncodingPlan.Create(texture, this.options);
        WriteHeaders(stream, plan);

        for (int level = 0; level < plan.MipLevelCount; level++)
        {
            TextureMipLevel mipLevel = texture.MipLevels[level];

            // File order is mip-major, then array element, then face, then depth slice, matching the
            // surface collection order established by the decoder.
            for (int layer = 0; layer < plan.Layers; layer++)
            {
                for (int face = 0; face < plan.FaceCount; face++)
                {
                    for (int slice = 0; slice < mipLevel.Depth; slice++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        int index = (((layer * plan.FaceCount) + face) * mipLevel.Depth) + slice;
                        WriteSurface(
                            texture.Configuration,
                            mipLevel.Surfaces[index],
                            plan.TexelFormat,
                            stream,
                            this.options);
                    }
                }
            }
        }
    }

    private static void WriteHeaders(Stream stream, DdsEncodingPlan plan)
    {
        Span<byte> headerBuffer = stackalloc byte[DdsConstants.HeaderIncludingMagicSize];
        BinaryPrimitives.WriteUInt32LittleEndian(headerBuffer, DdsConstants.MagicWord);
        plan.CreateHeader().WriteTo(headerBuffer[DdsConstants.MagicLength..]);
        stream.Write(headerBuffer);

        if (plan.HasDx10Header)
        {
            Span<byte> dx10Buffer = stackalloc byte[DdsConstants.Dx10HeaderSize];
            plan.CreateDx10Header().WriteTo(dx10Buffer);
            stream.Write(dx10Buffer);
        }
    }

    private static void WriteSurface(
        TextureConfiguration configuration,
        TextureSurface surface,
        TexelFormat targetFormat,
        Stream stream,
        DdsEncodingOptions encodingOptions)
    {
        if (surface.TexelFormat == targetFormat)
        {
            int expectedLength = DdsTexelFormats.GetEncodedByteCount(targetFormat, surface.Width, surface.Height);

            if (surface.PayloadLength != expectedLength)
            {
                throw new InvalidOperationException(
                    $"The '{surface.TexelFormat}' texel operations report {surface.PayloadLength} bytes for a "
                    + $"{surface.Width}x{surface.Height} surface but the DDS mapping requires {expectedLength} bytes.");
            }

            stream.Write(surface.GetPayload());
            return;
        }

        int length = DdsTexelFormats.GetEncodedByteCount(targetFormat, surface.Width, surface.Height);
        using IMemoryOwner<byte> buffer = configuration.ImageSharpConfiguration.MemoryAllocator.Allocate<byte>(length);
        Span<byte> destination = buffer.Memory.Span[..length];

        // Conversions between texel formats with different channel semantics always travel through RGBA,
        // and the texture itself is left untouched.
        using Image<Rgba32> image = surface.ToImage<Rgba32>();
        BcEncode.Encode(
            TexelFormatRegistry.GetOperations(targetFormat),
            image,
            destination,
            new BcEncodeOptions(encodingOptions.Bc1AlphaThreshold, encodingOptions.Bc7Quality));
        stream.Write(destination);
    }

    /// <summary>
    ///     The validated shape and format of the file about to be written.
    /// </summary>
    private sealed record DdsEncodingPlan(
        TexelFormat TexelFormat,
        bool HasDx10Header,
        DdsFourCc FourCc,
        int MipLevelCount,
        int Width,
        int Height,
        int Depth,
        int Layers,
        int FaceCount,
        bool IsVolume,
        DdsAlphaMode AlphaMode)
    {
        internal static DdsEncodingPlan Create(Texture texture, DdsEncodingOptions options)
        {
            if (texture.MipLevels.Count == 0)
            {
                throw new InvalidOperationException("The texture does not contain any mip levels to encode.");
            }

            TextureMipLevel baseLevel = texture.MipLevels[0];

            if (baseLevel.Surfaces.Count == 0)
            {
                throw new InvalidOperationException("The base mip level does not contain any surfaces to encode.");
            }

            TexelFormat texelFormat = options.TargetTexelFormat ?? baseLevel.Surfaces[0].TexelFormat;

            if (DdsTexelFormats.GetDxgiFormat(texelFormat) == DxgiFormat.Unknown)
            {
                throw new NotSupportedException(
                    $"The texel format '{texelFormat}' cannot be written to a DDS container.");
            }

            DdsTextureMetadata? metadata = texture.Metadata.TryGetFormatMetadata(
                DdsFormat.Instance,
                out DdsTextureMetadata? stored)
                ? stored
                : null;

            // The face/layer layout may have been declared by another container's metadata (a texture loaded
            // from KTX2 and saved as DDS), so it is resolved through the container-neutral seam.
            TextureLayout layout = TextureLayoutResolver.Resolve(texture);
            bool isVolume = baseLevel.Depth > 1;

            if (isVolume && layout.FaceCount != 1)
            {
                throw new InvalidOperationException(
                    $"The '{layout.Source}' texture metadata declares {layout.FaceCount} cube map faces, but the base "
                    + $"level has depth {baseLevel.Depth} and is a volume texture.");
            }

            bool isCubeMap = !isVolume && layout.IsCubeMap;
            int faceCount = isCubeMap ? 6 : 1;

            // Layers are a layout fact, not a metadata claim: the surfaces and the faces divide them.
            long surfacesPerLayer = (long)faceCount * baseLevel.Depth;
            int layers = isVolume ? 1 : (int)(baseLevel.Surfaces.Count / surfacesPerLayer);

            if (surfacesPerLayer == 0 || baseLevel.Surfaces.Count % surfacesPerLayer != 0)
            {
                throw new InvalidOperationException(
                    $"The base level holds {baseLevel.Surfaces.Count} surfaces, which does not divide into "
                    + $"{faceCount} face(s) x {baseLevel.Depth} slice(s) per layer."
                    + (layout.HasClaim
                        ? $" The face count comes from the '{layout.Source}' texture metadata."
                        : string.Empty));
            }

            if (layers <= 0)
            {
                throw new InvalidOperationException("The base mip level does not contain any surfaces to encode.");
            }

            for (int level = 0; level < texture.MipLevels.Count; level++)
            {
                TextureMipLevel mipLevel = texture.MipLevels[level];
                int expectedWidth = Math.Max(1, baseLevel.Width >> level);
                int expectedHeight = Math.Max(1, baseLevel.Height >> level);
                int expectedDepth = isVolume ? Math.Max(1, baseLevel.Depth >> level) : 1;

                if (mipLevel.Width != expectedWidth || mipLevel.Height != expectedHeight)
                {
                    throw new InvalidOperationException(
                        $"DDS requires a halving mip chain: level {level} is {mipLevel.Width}x{mipLevel.Height} but "
                        + $"{expectedWidth}x{expectedHeight} is required for a {baseLevel.Width}x{baseLevel.Height} base level.");
                }

                if (mipLevel.Depth != expectedDepth)
                {
                    throw new InvalidOperationException(
                        $"DDS requires a halving mip chain: level {level} has depth {mipLevel.Depth} but depth "
                        + $"{expectedDepth} is required for a base depth of {baseLevel.Depth}.");
                }

                int expectedSurfaces = layers * faceCount * expectedDepth;
                if (mipLevel.Surfaces.Count != expectedSurfaces)
                {
                    throw new InvalidOperationException(
                        $"Level {level} holds {mipLevel.Surfaces.Count} surfaces but the {layers} layer(s) x "
                        + $"{faceCount} face(s) x {expectedDepth} slice(s) layout requires {expectedSurfaces}.");
                }

                for (int index = 0; index < mipLevel.Surfaces.Count; index++)
                {
                    TexelFormat surfaceFormat = mipLevel.Surfaces[index].TexelFormat;

                    if (options.TargetTexelFormat is null && surfaceFormat != texelFormat)
                    {
                        throw new InvalidOperationException(
                            $"Level {level} surface {index} uses the '{surfaceFormat}' texel format while the first "
                            + $"surface uses '{texelFormat}'; set DdsEncodingOptions.TargetTexelFormat to convert them.");
                    }
                }
            }

            bool hasLegacyFourCc = DdsTexelFormats.TryGetFourCc(texelFormat, out DdsFourCc fourCc);

            // A legacy header has nowhere to store an array size, so an array (or a cube map array) must use
            // the DX10 header even when the caller asked for the legacy form. Cubes and volumes are expressible
            // through caps2 and stay legacy. This is the "legacy when expressible, DX10 otherwise" contract of
            // DdsEncodingOptions.UseDx10Header.
            bool useLegacyFourCc = !options.UseDx10Header && hasLegacyFourCc && layers <= 1;

            DdsAlphaMode alphaMode = metadata is not null && metadata.AlphaMode != DdsAlphaMode.Unknown
                ? metadata.AlphaMode
                : GetDefaultAlphaMode(texelFormat);

            return new DdsEncodingPlan(
                texelFormat,
                !useLegacyFourCc,
                useLegacyFourCc ? fourCc : DdsFourCc.None,
                texture.MipLevels.Count,
                baseLevel.Width,
                baseLevel.Height,
                isVolume ? baseLevel.Depth : 1,
                layers,
                faceCount,
                isVolume,
                alphaMode);
        }

        /// <summary>
        ///     Creates the <c>DDS_HEADER</c> for this plan.
        /// </summary>
        /// <returns>The header to write.</returns>
        internal DdsHeader CreateHeader()
        {
            bool isBlockCompressed = DdsTexelFormats.IsBlockCompressed(this.TexelFormat);
            int topLevelBytes = DdsTexelFormats.GetEncodedByteCount(this.TexelFormat, this.Width, this.Height);

            DdsFlags flags = DdsFlags.Caps
                             | DdsFlags.Height
                             | DdsFlags.Width
                             | DdsFlags.PixelFormat
                             | DdsFlags.MipMapCount;

            flags |= isBlockCompressed ? DdsFlags.LinearSize : DdsFlags.Pitch;

            if (this.IsVolume)
            {
                flags |= DdsFlags.Depth;
            }

            DdsCaps1 caps1 = DdsCaps1.Texture;

            if (this.MipLevelCount > 1 || this.FaceCount == 6 || this.IsVolume || this.Layers > 1)
            {
                caps1 |= DdsCaps1.Complex;
            }

            if (this.MipLevelCount > 1)
            {
                caps1 |= DdsCaps1.MipMap;
            }

            DdsCaps2 caps2 = DdsCaps2.Unknown;

            if (this.FaceCount == 6)
            {
                caps2 |= DdsCaps2.Cubemap | DdsCaps2.CubemapAllFaces;
            }

            if (this.IsVolume)
            {
                caps2 |= DdsCaps2.Volume;
            }

            uint fourCcValue = this.HasDx10Header ? (uint)DdsFourCc.DX10 : (uint)this.FourCc;
            DdsPixelFormat pixelFormat = new(
                DdsConstants.PixelFormatSize,
                DdsPixelFormatFlags.FourCC,
                fourCcValue,
                0);

            int pitchOrLinearSize = isBlockCompressed ? topLevelBytes : this.Width * 4;

            return new DdsHeader(
                DdsConstants.HeaderSize,
                flags,
                (uint)this.Height,
                (uint)this.Width,
                (uint)pitchOrLinearSize,
                this.IsVolume ? (uint)this.Depth : 1U,
                (uint)this.MipLevelCount,
                pixelFormat,
                caps1,
                caps2);
        }

        /// <summary>
        ///     Creates the <c>DDS_HEADER_DXT10</c> for this plan.
        /// </summary>
        /// <returns>The DX10 header to write.</returns>
        internal DdsHeaderDxt10 CreateDx10Header()
            => new(
                DdsTexelFormats.GetDxgiFormat(this.TexelFormat),
                this.IsVolume ? D3d10ResourceDimension.Texture3D : D3d10ResourceDimension.Texture2D,
                this.FaceCount == 6 ? D3d10ResourceMiscFlags.TextureCube : D3d10ResourceMiscFlags.Unknown,
                (uint)this.Layers,
                (uint)this.AlphaMode);

        private static DdsAlphaMode GetDefaultAlphaMode(TexelFormat texelFormat)
        {
            // texconv writes DDS_ALPHA_MODE_UNKNOWN (0) for block compressed data, including BC7 and the sRGB
            // variants, and DDS_ALPHA_MODE_STRAIGHT for the uncompressed RGBA path.
            if (texelFormat == TexelFormat.R8G8B8A8_UNorm)
            {
                return DdsAlphaMode.Straight;
            }

            return DdsAlphaMode.Unknown;
        }
    }
}
