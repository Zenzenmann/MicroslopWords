using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using HarfBuzzSharp;
using MicroslopWords.Core.Diagnostics;

namespace MicroslopWords.Layout;

public sealed class TextShaper : IDisposable
{
    public const double FallbackAdvanceRatio = 0.5;

    private readonly FontCatalog _catalog;
    private readonly HarfBuzzSharp.Buffer _buffer = new();
    private readonly Dictionary<FontKey, Font> _fonts = new();

    public TextShaper(FontCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
        _buffer.ClusterLevel = ClusterLevel.MonotoneCharacters;
    }

    public FontCatalog Catalog => _catalog;

    public ShapedText Shape(string text, FontKey request, double fontSizePoints)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSizePoints);

        if (text.Length == 0)
        {
            return new ShapedText(
                text,
                request,
                0,
                ImmutableArray<GlyphCluster>.Empty,
                ImmutableArray<Diagnostic>.Empty);
        }

        if (!_catalog.TryResolve(request, out var face))
        {
            var fallback = BuildFallbackClusters(text, fontSizePoints);

            return new ShapedText(
                text,
                request,
                fallback.Advance,
                fallback.Clusters,
                [new Diagnostic(
                    LayoutDiagnosticCodes.FontSubstituted,
                    DiagnosticSeverity.Warning,
                    $"No font available for {request.Family}; text was measured with the default fallback.")]);
        }

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        if (!string.Equals(face.Family, request.Family, StringComparison.Ordinal))
        {
            diagnostics.Add(new Diagnostic(
                LayoutDiagnosticCodes.FontSubstituted,
                DiagnosticSeverity.Warning,
                $"Font {request.Family} is unavailable; substituted with {face.Family}."));
        }

        var upem = face.UnitsPerEm <= 0 ? 1000 : face.UnitsPerEm;
        var scale = fontSizePoints / upem;
        var font = GetFont(face, upem);

        foreach (var rune in text.EnumerateRunes())
        {
            if (!Rune.IsControl(rune)
                && rune.Value != ' '
                && !FontFace.TryGetGlyph(font, (uint)rune.Value, out _))
            {
                diagnostics.Add(new Diagnostic(
                    LayoutDiagnosticCodes.FontMissingGlyph,
                    DiagnosticSeverity.Warning,
                    $"Font {face.Family} has no glyph for U+{rune.Value:X4}."));
                break;
            }
        }

        ShapeViaHarfBuzz(font, text, scale, out var advance, out var clusters);

        return new ShapedText(
            text,
            face.Key,
            advance,
            clusters,
            Deduplicate(diagnostics));
    }

    public void Dispose()
    {
        foreach (var font in _fonts.Values)
        {
            font.Dispose();
        }

        _buffer.Dispose();
    }

    private Font GetFont(FontFace face, int upem)
    {
        if (!_fonts.TryGetValue(face.Key, out var font))
        {
            font = new Font(face.HBFace);
            font.SetScale(upem, upem);
            _fonts[face.Key] = font;
        }

        return font;
    }

    private void ShapeViaHarfBuzz(
        Font font,
        string text,
        double scale,
        out double advance,
        out ImmutableArray<GlyphCluster> clusters)
    {
        _buffer.ClearContents();
        _buffer.AddUtf16(text, 0, text.Length);
        _buffer.Direction = Direction.LeftToRight;
        _buffer.Language = new Language(CultureInfo.InvariantCulture.Name);
        _buffer.Script = Script.Parse(text);
        font.Shape(_buffer, features: null);

        var infos = _buffer.GlyphInfos;
        var positions = _buffer.GlyphPositions;

        var clusterBuilder = ImmutableArray.CreateBuilder<GlyphCluster>();
        var totalAdvance = 0d;

        for (var index = 0; index < infos.Length; index++)
        {
            var info = infos[index];
            var position = positions[index];
            var glyphAdvance = position.XAdvance * scale;

            int textStart = (int)info.Cluster;
            int textLength = index + 1 < infos.Length
                ? (int)infos[index + 1].Cluster - textStart
                : text.Length - textStart;

            if (textLength <= 0)
            {
                textLength = 1;
            }

            clusterBuilder.Add(new GlyphCluster(textStart, textLength, glyphAdvance));
            totalAdvance += glyphAdvance;
        }

        advance = totalAdvance;
        clusters = clusterBuilder.ToImmutable();
    }

    private static (ImmutableArray<GlyphCluster> Clusters, double Advance) BuildFallbackClusters(
        string text,
        double fontSizePoints)
    {
        var advancePerRune = fontSizePoints * FallbackAdvanceRatio;
        var clusters = ImmutableArray.CreateBuilder<GlyphCluster>();
        var offset = 0;
        var totalAdvance = 0d;

        foreach (var rune in text.EnumerateRunes())
        {
            clusters.Add(new GlyphCluster(offset, rune.Utf16SequenceLength, advancePerRune));
            offset += rune.Utf16SequenceLength;
            totalAdvance += advancePerRune;
        }

        return (clusters.ToImmutable(), totalAdvance);
    }

    private static ImmutableArray<Diagnostic> Deduplicate(
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var diagnostic in diagnostics)
        {
            if (seen.Add(diagnostic.Code + "|" + diagnostic.Message))
            {
                result.Add(diagnostic);
            }
        }

        return result.ToImmutable();
    }
}
