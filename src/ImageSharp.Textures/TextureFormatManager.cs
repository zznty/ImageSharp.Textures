namespace ImageSharp.Textures;

/// <summary>
///     Manages the texture formats, decoders, encoders and format detectors known to a <see cref="TextureConfiguration" />
///     .
/// </summary>
public sealed class TextureFormatManager : ITextureFormatManager
{
    private readonly List<Registration> registrations = [];
    private readonly Dictionary<Type, Registration> registrationsByType = [];

    /// <inheritdoc />
    public IEnumerable<ITextureFormat> Formats
    {
        get
        {
            foreach (Registration registration in this.registrations)
            {
                yield return registration.Format;
            }
        }
    }

    /// <inheritdoc />
    public ITextureDecoder GetDecoder(ITextureFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);

        if (!this.registrationsByType.TryGetValue(format.GetType(), out Registration? registration)
            || registration.Decoder is null)
        {
            throw new NotSupportedException($"No decoder is registered for the texture format '{format.Name}'.");
        }

        return registration.Decoder;
    }

    /// <inheritdoc />
    public bool TryGetEncoder(ITextureFormat format, out ITextureEncoder? encoder)
    {
        ArgumentNullException.ThrowIfNull(format);
        encoder = null;

        if (this.registrationsByType.TryGetValue(format.GetType(), out Registration? registration))
        {
            encoder = registration.Encoder;
        }

        return encoder is not null;
    }

    /// <inheritdoc />
    public ITextureFormat? FindFormatByFileExtension(string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        string normalized = extension.StartsWith('.') ? extension[1..] : extension;

        if (normalized.Length == 0)
        {
            return null;
        }

        foreach (Registration registration in this.registrations)
        {
            foreach (string candidate in registration.Format.FileExtensions)
            {
                string candidateExtension = candidate.StartsWith('.') ? candidate[1..] : candidate;
                if (candidateExtension.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return registration.Format;
                }
            }
        }

        return null;
    }

    /// <inheritdoc />
    public ITextureFormat? FindFormatByMimeType(string mimeType)
    {
        ArgumentNullException.ThrowIfNull(mimeType);

        if (mimeType.Length == 0)
        {
            return null;
        }

        foreach (Registration registration in this.registrations)
        {
            ITextureFormat format = registration.Format;
            if (format.DefaultMimeType.Equals(mimeType, StringComparison.OrdinalIgnoreCase))
            {
                return format;
            }

            foreach (string candidate in format.MimeTypes)
            {
                if (candidate.Equals(mimeType, StringComparison.OrdinalIgnoreCase))
                {
                    return format;
                }
            }
        }

        return null;
    }

    /// <inheritdoc />
    public ITextureFormat? DetectFormat(ReadOnlySpan<byte> header)
    {
        foreach (Registration registration in this.registrations)
        {
            if (registration.Detector is { } detector
                && detector.DetectFormat(header) is { } format)
            {
                return format;
            }
        }

        return null;
    }

    /// <summary>
    ///     Registers a texture format together with its decoder and optional encoder.
    ///     Registering a format of the same type as an existing registration replaces it.
    /// </summary>
    /// <param name="format">The texture format.</param>
    /// <param name="decoder">The decoder for the format.</param>
    /// <param name="encoder">The encoder for the format, when encoding is supported.</param>
    public void AddFormat(ITextureFormat format, ITextureDecoder decoder, ITextureEncoder? encoder = null)
    {
        ArgumentNullException.ThrowIfNull(decoder);
        this.AddFormat(format, decoder, encoder, null);
    }

    /// <summary>
    ///     Removes the registration for the given format.
    /// </summary>
    /// <param name="format">The texture format.</param>
    /// <returns><see langword="true" /> when a registration was removed; otherwise, <see langword="false" />.</returns>
    public bool RemoveFormat(ITextureFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);

        if (!this.registrationsByType.Remove(format.GetType(), out Registration? registration))
        {
            return false;
        }

        this.registrations.Remove(registration);
        return true;
    }

    /// <summary>
    ///     Registers a texture format, decoder, encoder and detector. Codecs inside this assembly use this
    ///     overload to publish the format detector alongside the codec.
    /// </summary>
    /// <param name="format">The texture format.</param>
    /// <param name="decoder">The decoder for the format, when decoding is supported.</param>
    /// <param name="encoder">The encoder for the format, when encoding is supported.</param>
    /// <param name="detector">The format detector, when the format can be detected from header bytes.</param>
    internal void AddFormat(
        ITextureFormat format,
        ITextureDecoder? decoder,
        ITextureEncoder? encoder,
        ITextureFormatDetector? detector)
    {
        ArgumentNullException.ThrowIfNull(format);

        if (decoder is not null && decoder.Format != format)
        {
            throw new ArgumentException("The decoder must handle the registered format.", nameof(decoder));
        }

        if (encoder is not null && encoder.Format != format)
        {
            throw new ArgumentException("The encoder must handle the registered format.", nameof(encoder));
        }

        if (this.registrationsByType.TryGetValue(format.GetType(), out Registration? existing))
        {
            this.registrations.Remove(existing);
        }

        Registration registration = new(format, decoder, encoder, detector);
        this.registrations.Add(registration);
        this.registrationsByType[format.GetType()] = registration;
    }

    private sealed class Registration(
        ITextureFormat format,
        ITextureDecoder? decoder,
        ITextureEncoder? encoder,
        ITextureFormatDetector? detector)
    {
        public ITextureFormat Format { get; } = format;

        public ITextureDecoder? Decoder { get; } = decoder;

        public ITextureEncoder? Encoder { get; } = encoder;

        public ITextureFormatDetector? Detector { get; } = detector;
    }
}
