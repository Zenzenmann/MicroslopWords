using HarfBuzzSharp;

namespace MicroslopWords.Layout;

public sealed class FontFace : IDisposable
{
    private readonly MemoryStream _stream;
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
        _stream = new MemoryStream(fontData, writable: false);

        try
        {
            _blob = Blob.FromStream(_stream);
            _face = new Face(_blob, 0);
        }
        catch (DllNotFoundException)
        {
            _stream.Dispose();
            throw;
        }

        UnitsPerEm = _face.UnitsPerEm;

        if (GlyphCount <= 0)
        {
            _face.Dispose();
            _blob.Dispose();
            _stream.Dispose();

            throw new ArgumentException(
                "Font data does not contain any glyphs.",
                nameof(fontData));
        }
    }

    public string Family { get; }

    public FontWeight Weight { get; }

    public FontSlant Slant { get; }

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
        _stream.Dispose();
    }
}
