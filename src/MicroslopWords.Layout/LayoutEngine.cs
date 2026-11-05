using System.Collections.Immutable;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Styles;

namespace MicroslopWords.Layout;

public sealed class LayoutEngine
{
    private readonly FontCatalog _fontCatalog;

    public LayoutEngine(FontCatalog? fontCatalog = null)
    {
        _fontCatalog = fontCatalog ?? FontCatalog.Empty;
    }

    public FontCatalog FontCatalog => _fontCatalog;

    public DocumentLayout Layout(
        Document document,
        FontCatalog? fontCatalog = null,
        PageSettings? pageSettings = null,
        LayoutConstraints? constraints = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var catalog = fontCatalog ?? _fontCatalog;
        var defaultPage = pageSettings
            ?? document.Sections.FirstOrDefault()?.EffectivePage
            ?? PageSettings.Letter;
        var activePage = defaultPage;
        var contentWidth = constraints?.MaxWidthPoints ?? defaultPage.ContentWidthPoints;
        var contentHeight = constraints?.MaxHeightPoints ?? defaultPage.ContentHeightPoints;
        var pageBottom = activePage.MarginTopPoints + contentHeight;

        using var shaper = new TextShaper(catalog);
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var pages = ImmutableArray.CreateBuilder<PageLayout>();
        var paragraphs = ImmutableArray.CreateBuilder<ParagraphLayout>();
        var pageNumber = 1;
        var cursorY = activePage.MarginTopPoints;

        void FlushPage()
        {
            if (paragraphs.Count == 0)
            {
                return;
            }

            pages.Add(new PageLayout(
                pageNumber,
                new LayoutRect(
                    0,
                    0,
                    activePage.EffectiveWidthPoints,
                    activePage.EffectiveHeightPoints),
                paragraphs.ToImmutable()));

            paragraphs.Clear();
            pageNumber++;
            cursorY = activePage.MarginTopPoints;
        }

        foreach (var section in document.Sections)
        {
            if (paragraphs.Count > 0)
            {
                FlushPage();
            }

            activePage = pageSettings ?? section.EffectivePage;
            contentWidth = constraints?.MaxWidthPoints ?? activePage.ContentWidthPoints;
            contentHeight = constraints?.MaxHeightPoints ?? activePage.ContentHeightPoints;
            pageBottom = activePage.MarginTopPoints + contentHeight;
            cursorY = activePage.MarginTopPoints;

            foreach (var paragraph in section.Blocks.OfType<ParagraphBlock>())
            {
                var fontSize = paragraph.Runs.Length == 0
                    ? RunProperties.Default.FontSizePoints
                    : paragraph.Runs.Max(run => run.EffectiveProperties.FontSizePoints);
                var lineHeight = fontSize * paragraph.EffectiveProperties.LineSpacing;
                var request = ResolveFontRequest(paragraph);
                var spaceBefore = paragraph.EffectiveProperties.SpaceBeforePoints;
                cursorY += spaceBefore;

                var paragraphLines = ImmutableArray.CreateBuilder<LineLayout>();
                var maxLineWidth = 0d;

                void FinishParagraphSegment()
                {
                    if (paragraphLines.Count == 0)
                    {
                        return;
                    }

                    var paragraphTop = paragraphLines[0].Bounds.Y;
                    var paragraphHeight = Math.Max(lineHeight, paragraphLines.Count * lineHeight);

                    paragraphs.Add(new ParagraphLayout(
                        paragraph.Id,
                        new LayoutRect(activePage.MarginLeftPoints, paragraphTop, maxLineWidth, paragraphHeight),
                        paragraphLines.ToImmutable()));

                    paragraphLines.Clear();
                    maxLineWidth = 0;
                }

                var segments = SplitSoftBreakSegments(paragraph.PlainText);

                for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
                {
                    var segment = segments[segmentIndex];
                    var shaped = shaper.Shape(segment.Text, request, fontSize);

                    foreach (var diagnostic in shaped.Diagnostics)
                    {
                        diagnostics.Add(diagnostic);
                    }

                    var lines = WrapShapedText(shaped, segment.Start, contentWidth);

                    for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                    {
                        var line = lines[lineIndex];

                        if (cursorY + lineHeight > pageBottom
                            && (paragraphs.Count > 0 || paragraphLines.Count > 0))
                        {
                            FinishParagraphSegment();
                            FlushPage();
                        }

                        maxLineWidth = Math.Max(maxLineWidth, line.AdvanceWidthPoints);
                        paragraphLines.Add(new LineLayout(
                            line.Text,
                            line.TextStart,
                            segmentIndex > 0 && lineIndex == 0,
                            new LayoutRect(activePage.MarginLeftPoints, cursorY, line.AdvanceWidthPoints, lineHeight),
                            fontSize,
                            line.Clusters,
                            shaped.ResolvedFont));
                        cursorY += lineHeight;
                    }
                }

                FinishParagraphSegment();
                cursorY += paragraph.EffectiveProperties.SpaceAfterPoints;
            }
        }

        FlushPage();

        if (pages.Count == 0)
        {
            pages.Add(new PageLayout(
                1,
                new LayoutRect(
                    0,
                    0,
                    defaultPage.EffectiveWidthPoints,
                    defaultPage.EffectiveHeightPoints),
                ImmutableArray<ParagraphLayout>.Empty));
        }

        foreach (var diagnostic in catalog.Diagnostics)
        {
            diagnostics.Add(diagnostic);
        }

        return new DocumentLayout(
            pages.ToImmutable(),
            defaultPage.EffectiveWidthPoints,
            defaultPage.EffectiveHeightPoints,
            Deduplicate(diagnostics));
    }

    private static FontKey ResolveFontRequest(ParagraphBlock paragraph)
    {
        var properties = paragraph.Runs.Length > 0
            ? paragraph.Runs[0].EffectiveProperties
            : RunProperties.Default;

        return new FontKey(
            properties.FontFamily,
            properties.Bold ? FontWeight.Bold : FontWeight.Regular,
            properties.Italic ? FontSlant.Italic : FontSlant.Upright);
    }

    private static ImmutableArray<ShapedLine> WrapShapedText(
        ShapedText shaped,
        int paragraphStart,
        double maxWidth)
    {
        if (shaped.Text.Length == 0)
        {
            return
            [
                new ShapedLine(
                    string.Empty,
                    paragraphStart,
                    0,
                    shaped.ResolvedFont,
                    ImmutableArray<GlyphCluster>.Empty),
            ];
        }

        var clusters = shaped.Clusters;

        if (clusters.Length == 0)
        {
            return
            [
                new ShapedLine(
                    shaped.Text,
                    paragraphStart,
                    0,
                    shaped.ResolvedFont,
                    ImmutableArray<GlyphCluster>.Empty),
            ];
        }

        var lines = ImmutableArray.CreateBuilder<ShapedLine>();
        var lineStartCluster = 0;
        var currentWidth = 0d;
        var lastBreak = -1;

        for (var index = 0; index < clusters.Length; index++)
        {
            currentWidth += clusters[index].Advance;

            if (IsBreakOpportunity(shaped.Text, clusters[index]))
            {
                lastBreak = index;
            }

            if (currentWidth <= maxWidth || index <= lineStartCluster)
            {
                continue;
            }

            var breakAt = lastBreak >= lineStartCluster ? lastBreak : index - 1;
            lines.Add(CommitLine(shaped, paragraphStart, lineStartCluster, breakAt));

            lineStartCluster = breakAt + 1;
            currentWidth = 0;
            lastBreak = -1;

            for (var replay = lineStartCluster; replay <= index; replay++)
            {
                currentWidth += clusters[replay].Advance;

                if (IsBreakOpportunity(shaped.Text, clusters[replay]))
                {
                    lastBreak = replay;
                }
            }
        }

        lines.Add(CommitLine(
            shaped,
            paragraphStart,
            lineStartCluster,
            clusters.Length - 1));

        return lines.ToImmutable();
    }

    private static ShapedLine CommitLine(
        ShapedText shaped,
        int paragraphStart,
        int startCluster,
        int endCluster)
    {
        var clusters = shaped.Clusters;
        var first = clusters[startCluster];
        var last = clusters[endCluster];
        var textStart = first.TextStart;
        var textLength = (last.TextStart + last.TextLength) - textStart;
        var width = 0d;
        var built = ImmutableArray.CreateBuilder<GlyphCluster>(
            endCluster - startCluster + 1);

        for (var index = startCluster; index <= endCluster; index++)
        {
            var cluster = clusters[index];
            width += cluster.Advance;
            built.Add(new GlyphCluster(
                cluster.TextStart + paragraphStart,
                cluster.TextLength,
                cluster.Advance));
        }

        return new ShapedLine(
            shaped.Text.Substring(textStart, textLength),
            paragraphStart + textStart,
            width,
            shaped.ResolvedFont,
            built.ToImmutable());
    }

    private static bool IsBreakOpportunity(string text, GlyphCluster cluster) =>
        cluster.TextStart >= 0
        && cluster.TextStart < text.Length
        && char.IsWhiteSpace(text[cluster.TextStart]);

    private static ImmutableArray<(int Start, string Text)> SplitSoftBreakSegments(
        string text)
    {
        var segments = ImmutableArray.CreateBuilder<(int Start, string Text)>();
        var start = 0;

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\n')
            {
                continue;
            }

            segments.Add((start, text[start..index]));
            start = index + 1;
        }

        segments.Add((start, text[start..]));

        return segments.ToImmutable();
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

    private readonly record struct ShapedLine(
        string Text,
        int TextStart,
        double AdvanceWidthPoints,
        FontKey ResolvedFont,
        ImmutableArray<GlyphCluster> Clusters);
}
