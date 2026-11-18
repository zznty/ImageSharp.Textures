using System.Collections;

namespace ImageSharp.Textures;

/// <summary>
///     Represents the ordered mip levels in a texture.
/// </summary>
public sealed class TextureMipLevelCollection : IDisposable, IEnumerable<TextureMipLevel>
{
    private readonly List<TextureMipLevel> mipLevels = [];

    private bool isDisposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TextureMipLevelCollection" /> class.
    /// </summary>
    internal TextureMipLevelCollection()
    {
    }

    /// <summary>
    ///     Gets the number of mip levels.
    /// </summary>
    public int Count => this.mipLevels.Count;

    /// <summary>
    ///     Gets the base mip level.
    /// </summary>
    /// <exception cref="InvalidOperationException">The collection is empty.</exception>
    public TextureMipLevel BaseLevel
        => this.mipLevels.Count > 0
            ? this.mipLevels[0]
            : throw new InvalidOperationException("The texture does not contain any mip levels.");

    /// <summary>
    ///     Gets the mip level at the specified index.
    /// </summary>
    /// <param name="index">The mip level index.</param>
    /// <returns>The mip level at the specified index.</returns>
    public TextureMipLevel this[int index] => this.mipLevels[index];

    /// <summary>
    ///     Releases resources owned by the collection.
    /// </summary>
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        foreach (TextureMipLevel mipLevel in this.mipLevels)
        {
            mipLevel.Dispose();
        }

        this.mipLevels.Clear();
        this.isDisposed = true;
    }

    /// <summary>
    ///     Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>The enumerator.</returns>
    public IEnumerator<TextureMipLevel> GetEnumerator() => this.mipLevels.GetEnumerator();

    /// <summary>
    ///     Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>The enumerator.</returns>
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    /// <summary>
    ///     Determines the index of a mip level in the collection.
    /// </summary>
    /// <param name="mipLevel">The mip level to locate.</param>
    /// <returns>The index of the mip level if found; otherwise, -1.</returns>
    public int IndexOf(TextureMipLevel mipLevel) => this.mipLevels.IndexOf(mipLevel);

    /// <summary>
    ///     Determines whether the collection contains the specified mip level.
    /// </summary>
    /// <param name="mipLevel">The mip level to locate.</param>
    /// <returns><see langword="true" /> if the collection contains the mip level; otherwise, <see langword="false" />.</returns>
    public bool Contains(TextureMipLevel mipLevel) => this.mipLevels.Contains(mipLevel);

    /// <summary>
    ///     Clones and inserts a mip level at the specified index.
    /// </summary>
    /// <param name="index">The mip level index.</param>
    /// <param name="source">The mip level to clone and insert.</param>
    /// <returns>The inserted mip level.</returns>
    public TextureMipLevel InsertMipLevel(int index, TextureMipLevel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ObjectDisposedException.ThrowIf(this.isDisposed, this);
        TextureMipLevel clone = source.Clone();
        this.mipLevels.Insert(index, clone);
        return clone;
    }

    /// <summary>
    ///     Clones and appends a mip level to the collection.
    /// </summary>
    /// <param name="source">The mip level to clone and append.</param>
    /// <returns>The appended mip level.</returns>
    public TextureMipLevel AddMipLevel(TextureMipLevel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ObjectDisposedException.ThrowIf(this.isDisposed, this);
        TextureMipLevel clone = source.Clone();
        this.mipLevels.Add(clone);
        return clone;
    }

    /// <summary>
    ///     Removes the mip level at the specified index and releases its resources.
    /// </summary>
    /// <param name="index">The mip level index.</param>
    public void RemoveMipLevel(int index)
    {
        TextureMipLevel mipLevel = this.mipLevels[index];
        this.mipLevels.RemoveAt(index);
        mipLevel.Dispose();
    }

    /// <summary>
    ///     Moves a mip level from one index to another.
    /// </summary>
    /// <param name="sourceIndex">The source mip level index.</param>
    /// <param name="destinationIndex">The destination mip level index.</param>
    public void MoveMipLevel(int sourceIndex, int destinationIndex)
    {
        TextureMipLevel mipLevel = this.mipLevels[sourceIndex];
        this.mipLevels.RemoveAt(sourceIndex);
        this.mipLevels.Insert(destinationIndex, mipLevel);
    }

    /// <summary>
    ///     Removes the mip level at the specified index and transfers ownership to the caller.
    /// </summary>
    /// <param name="index">The mip level index.</param>
    /// <returns>The removed mip level.</returns>
    public TextureMipLevel ExportMipLevel(int index)
    {
        TextureMipLevel mipLevel = this.mipLevels[index];
        this.mipLevels.RemoveAt(index);
        return mipLevel;
    }

    /// <summary>
    ///     Creates a new mip level containing clones of the surfaces at the specified index.
    /// </summary>
    /// <param name="index">The mip level index.</param>
    /// <returns>The cloned mip level.</returns>
    public TextureMipLevel CloneMipLevel(int index) => this.mipLevels[index].Clone();

    /// <summary>
    ///     Appends an already owned mip level without cloning it. Decoders use this to hand over freshly
    ///     allocated levels without a redundant payload copy.
    /// </summary>
    /// <param name="mipLevel">The mip level to append.</param>
    internal void AppendMipLevel(TextureMipLevel mipLevel)
    {
        ObjectDisposedException.ThrowIf(this.isDisposed, this);
        this.mipLevels.Add(mipLevel);
    }
}
