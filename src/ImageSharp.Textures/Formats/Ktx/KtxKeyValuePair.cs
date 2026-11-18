namespace ImageSharp.Textures.Formats.Ktx;

/// <summary>
///     Represents one key/value pair stored in the key/value data block of a KTX v1 or KTX2 file.
/// </summary>
public sealed class KtxKeyValuePair
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="KtxKeyValuePair" /> class.
    /// </summary>
    /// <param name="key">The UTF-8 key, without the NUL terminator.</param>
    /// <param name="value">The raw value bytes.</param>
    public KtxKeyValuePair(string key, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        this.Key = key;
        this.Value = value;
    }

    /// <summary>
    ///     Gets the UTF-8 key, without the NUL terminator.
    /// </summary>
    public string Key { get; }

    /// <summary>
    ///     Gets the raw value bytes.
    /// </summary>
    public byte[] Value { get; }

    /// <summary>
    ///     Creates a deep copy of the pair.
    /// </summary>
    /// <returns>The cloned pair.</returns>
    internal KtxKeyValuePair Clone() => new(this.Key, (byte[])this.Value.Clone());
}
