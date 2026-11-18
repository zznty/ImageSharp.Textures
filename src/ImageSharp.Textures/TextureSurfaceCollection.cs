using System.Collections;

namespace ImageSharp.Textures;

/// <summary>
///     Represents the ordered surfaces in a mip level.
/// </summary>
public sealed class TextureSurfaceCollection : IDisposable, IEnumerable<TextureSurface>
{
    private readonly TextureMipLevel owner;
    private readonly List<TextureSurface> surfaces = [];

    private bool isDisposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TextureSurfaceCollection" /> class.
    /// </summary>
    /// <param name="owner">The mip level that owns this collection.</param>
    internal TextureSurfaceCollection(TextureMipLevel owner) => this.owner = owner;

    /// <summary>
    ///     Gets the number of surfaces.
    /// </summary>
    public int Count => this.surfaces.Count;

    /// <summary>
    ///     Gets the surface at the specified index.
    /// </summary>
    /// <param name="index">The surface index.</param>
    /// <returns>The surface at the specified index.</returns>
    public TextureSurface this[int index] => this.surfaces[index];

    /// <summary>
    ///     Releases resources owned by the collection.
    /// </summary>
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        foreach (TextureSurface surface in this.surfaces)
        {
            surface.Dispose();
        }

        this.surfaces.Clear();
        this.isDisposed = true;
    }

    /// <summary>
    ///     Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>The enumerator.</returns>
    public IEnumerator<TextureSurface> GetEnumerator() => this.surfaces.GetEnumerator();

    /// <summary>
    ///     Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>The enumerator.</returns>
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    /// <summary>
    ///     Determines the index of a surface in the collection.
    /// </summary>
    /// <param name="surface">The surface to locate.</param>
    /// <returns>The index of the surface if found; otherwise, -1.</returns>
    public int IndexOf(TextureSurface surface) => this.surfaces.IndexOf(surface);

    /// <summary>
    ///     Determines whether the collection contains the specified surface.
    /// </summary>
    /// <param name="surface">The surface to locate.</param>
    /// <returns><see langword="true" /> if the collection contains the surface; otherwise, <see langword="false" />.</returns>
    public bool Contains(TextureSurface surface) => this.surfaces.Contains(surface);

    /// <summary>
    ///     Clones and inserts a surface at the specified index.
    /// </summary>
    /// <param name="index">The surface index.</param>
    /// <param name="source">The surface to clone and insert.</param>
    /// <returns>The inserted surface.</returns>
    /// <exception cref="ArgumentException">The source surface dimensions do not match the mip level dimensions.</exception>
    public TextureSurface InsertSurface(int index, TextureSurface source)
    {
        ArgumentNullException.ThrowIfNull(source);
        this.ValidateSurface(source);
        TextureSurface clone = source.Clone();
        this.surfaces.Insert(index, clone);
        return clone;
    }

    /// <summary>
    ///     Clones and appends a surface to the collection.
    /// </summary>
    /// <param name="source">The surface to clone and append.</param>
    /// <returns>The appended surface.</returns>
    /// <exception cref="ArgumentException">The source surface dimensions do not match the mip level dimensions.</exception>
    public TextureSurface AddSurface(TextureSurface source)
    {
        ArgumentNullException.ThrowIfNull(source);
        this.ValidateSurface(source);
        TextureSurface clone = source.Clone();
        this.surfaces.Add(clone);
        return clone;
    }

    /// <summary>
    ///     Removes the surface at the specified index and releases its resources.
    /// </summary>
    /// <param name="index">The surface index.</param>
    public void RemoveSurface(int index)
    {
        TextureSurface surface = this.surfaces[index];
        this.surfaces.RemoveAt(index);
        surface.Dispose();
    }

    /// <summary>
    ///     Moves a surface from one index to another.
    /// </summary>
    /// <param name="sourceIndex">The source surface index.</param>
    /// <param name="destinationIndex">The destination surface index.</param>
    public void MoveSurface(int sourceIndex, int destinationIndex)
    {
        TextureSurface surface = this.surfaces[sourceIndex];
        this.surfaces.RemoveAt(sourceIndex);
        this.surfaces.Insert(destinationIndex, surface);
    }

    /// <summary>
    ///     Removes the surface at the specified index and transfers ownership to the caller.
    /// </summary>
    /// <param name="index">The surface index.</param>
    /// <returns>The removed surface.</returns>
    public TextureSurface ExportSurface(int index)
    {
        TextureSurface surface = this.surfaces[index];
        this.surfaces.RemoveAt(index);
        return surface;
    }

    /// <summary>
    ///     Creates a clone of the surface at the specified index.
    /// </summary>
    /// <param name="index">The surface index.</param>
    /// <returns>The cloned surface.</returns>
    public TextureSurface CloneSurface(int index) => this.surfaces[index].Clone();

    /// <summary>
    ///     Appends an already owned surface without cloning it. Decoders use this to hand over freshly
    ///     allocated surfaces without a redundant payload copy.
    /// </summary>
    /// <param name="surface">The surface to append.</param>
    internal void AppendSurface(TextureSurface surface)
    {
        ObjectDisposedException.ThrowIf(this.isDisposed, this);
        this.ValidateSurface(surface);
        this.surfaces.Add(surface);
    }

    private void ValidateSurface(TextureSurface surface)
    {
        ObjectDisposedException.ThrowIf(this.isDisposed, this);

        if (surface.Width != this.owner.Width || surface.Height != this.owner.Height)
        {
            throw new ArgumentException(
                $"The source surface dimensions {surface.Width}x{surface.Height} do not match the mip level dimensions {this.owner.Width}x{this.owner.Height}.",
                nameof(surface));
        }
    }
}
