using System.Runtime.InteropServices;
using HarfBuzzSharp;

namespace MicroslopWords.Layout;

public sealed class FontFace : IDisposable
{
    private readonly Blob _blob;
    private readonly Face _face;

    public FontFace(string family, FontWeight weight, FontSlant slant, byte[] fontData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);
        ArgumentNullException.ThrowIfNull(fontData);

        if (fontData.Length == 0)
        {
            throw new ArgumentException("Font data is empty.", nameof(fontData));
        }

        Family = family;
        Weight = weight;
        Slant = slant;
        FontData = fontData;

        var handle = GCHandle.Alloc(fontData, GCHandleType.Pinned);

        try
        {
            // Duplicate mode makes HarfBuzz own a copy; Blob.FromStream keeps a
            // pointer to a collectible array and intermittently shapes freed memory.
            _blob = new Blob(handle.AddrOfPinnedObject(), fontData.Length, MemoryMode.Duplicate);
        }
        catch
        {
            _blob?.Dispose();
            throw;
        }
        finally
        {
            handle.Free();
        }

        try
        {
            _face = new Face(_blob, 0);
        }
        catch
        {
            _blob.Dispose();
            throw;
        }

        UnitsPerEm = _face.UnitsPerEm;

        if (GlyphCount <= 0)
        {
            _face.Dispose();
            _blob.Dispose();

            throw new ArgumentException(
                "Font data does not contain any glyphs.",
                nameof(fontData));
        }
    }

    public string Family { get; }

    public FontWeight Weight { get; }

    public FontSlant Slant { get; }

    /// <summary>
    /// The original font bytes supplied by the catalog source. Renderers use
    /// these to create platform typefaces for the same face layout measured.
    /// </summary>
    public byte[] FontData { get; }

    public int UnitsPerEm { get; }

    public int GlyphCount => _face.GlyphCount;

    public FontKey Key => new(Family, Weight, Slant);

    internal Face HBFace => _face;

    public static bool TryGetGlyph(Font font, uint codePoint, out uint glyph)
    {
        ArgumentNullException.ThrowIfNull(font);

        return font.TryGetNominalGlyph(codePoint, out glyph) && glyph != 0;
    }

    public void Dispose()
    {
        _face.Dispose();
        _blob.Dispose();
    }
}
