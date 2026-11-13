using MicroslopWords.Layout;
using SkiaSharp;

namespace MicroslopWords.Rendering;

internal sealed class SkiaFontResolver : IDisposable
{
    private readonly FontCatalog? _catalog;
    private readonly SKTypeface _fallback;
    private readonly Dictionary<FontKey, SKTypeface> _typefaces = [];
    private readonly List<SKData> _fontData = [];

    public SkiaFontResolver(
        FontCatalog? catalog,
        SKTypeface? fallback = null)
    {
        _catalog = catalog;
        _fallback = fallback ?? SKTypeface.Default;
    }

    public SKTypeface Resolve(FontKey key)
    {
        if (_typefaces.TryGetValue(key, out var cached))
        {
            return cached;
        }

        SKTypeface? typeface = null;

        if (_catalog is not null && _catalog.TryResolve(key, out var face))
        {
            var data = SKData.CreateCopy(face.FontData);
            _fontData.Add(data);
            typeface = SKTypeface.FromData(data);
        }

        typeface ??= _fallback;
        _typefaces[key] = typeface;

        return typeface;
    }

    public void Dispose()
    {
        foreach (var typeface in _typefaces.Values)
        {
            if (!ReferenceEquals(typeface, _fallback))
            {
                typeface.Dispose();
            }
        }

        foreach (var data in _fontData)
        {
            data.Dispose();
        }

        _typefaces.Clear();
        _fontData.Clear();
    }
}
