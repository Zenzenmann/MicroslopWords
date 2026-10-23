using System.Collections.Immutable;
using System.Text;
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
        var page = pageSettings ?? PageSettings.Letter;
        var contentWidth = constraints?.MaxWidthPoints ?? page.ContentWidthPoints;
        var contentHeight = constraints?.MaxHeightPoints ?? page.ContentHeightPoints;
        var pageBottom = page.MarginTopPoints + contentHeight;

        using var shaper = new TextShaper(catalog);
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var pages = ImmutableArray.CreateBuilder<PageLayout>();
        var paragraphs = ImmutableArray.CreateBuilder<ParagraphLayout>();
        var pageNumber = 1;
        var cursorY = page.MarginTopPoints;

        void FlushPage()
        {
            if (paragraphs.Count == 0)
            {
                return;
            }

            pages.Add(new PageLayout(
                pageNumber,
                new LayoutRect(0, 0, page.WidthPoints, page.HeightPoints),
                paragraphs.ToImmutable()));

            paragraphs.Clear();
            pageNumber++;
            cursorY = page.MarginTopPoints;
        }

        foreach (var section in document.Sections)
        {
            foreach (var paragraph in section.Blocks.OfType<ParagraphBlock>())
            {
                var fontSize = paragraph.Runs.Length == 0
                    ? RunProperties.Default.FontSizePoints
                    : paragraph.Runs.Max(run => run.EffectiveProperties.FontSizePoints);
                var lineHeight = fontSize * paragraph.EffectiveProperties.LineSpacing;
                var request = ResolveFontRequest(paragraph);
                var shaped = shaper.Shape(paragraph.PlainText, request, fontSize);

                foreach (var diagnostic in shaped.Diagnostics)
                {
                    diagnostics.Add(diagnostic);
                }

                var lines = WrapShapedText(shaped, contentWidth);
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
                        new LayoutRect(page.MarginLeftPoints, paragraphTop, maxLineWidth, paragraphHeight),
                        paragraphLines.ToImmutable()));

                    paragraphLines.Clear();
                    maxLineWidth = 0;
                }

                foreach (var line in lines)
                {
                    if (cursorY + lineHeight > pageBottom
                        && (paragraphs.Count > 0 || paragraphLines.Count > 0))
                    {
                        FinishParagraphSegment();
                        FlushPage();
                    }

                    maxLineWidth = Math.Max(maxLineWidth, line.AdvanceWidthPoints);
                    paragraphLines.Add(new LineLayout(
                        line.Text,
                        new LayoutRect(page.MarginLeftPoints, cursorY, line.AdvanceWidthPoints, lineHeight),
                        fontSize,
                        line.Clusters,
                        shaped.ResolvedFont));
                    cursorY += lineHeight;
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
                new LayoutRect(0, 0, page.WidthPoints, page.HeightPoints),
                ImmutableArray<ParagraphLayout>.Empty));
        }

        foreach (var diagnostic in catalog.Diagnostics)
        {
            diagnostics.Add(diagnostic);
        }

        return new DocumentLayout(
            pages.ToImmutable(),
            page.WidthPoints,
            page.HeightPoints,
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
        double maxWidth)
    {
        if (shaped.Text.Length == 0)
        {
            return ImmutableArray.Create(new ShapedLine(
                string.Empty,
                0,
                shaped.ResolvedFont,
                ImmutableArray<GlyphCluster>.Empty));
        }

        var lines = ImmutableArray.CreateBuilder<ShapedLine>();
        var currentText = new StringBuilder();
        var currentClusters = ImmutableArray.CreateBuilder<GlyphCluster>();
        var currentWidth = 0d;

        var wordBoundaries = FindWordBoundaries(shaped);

        foreach (var word in wordBoundaries)
        {
            var wordWidth = SumAdvance(shaped.Clusters, word.Start, word.End);

            if (currentWidth > 0
                && currentWidth + word.PrecedingSpaceWidth + wordWidth > maxWidth)
            {
                lines.Add(Commit(currentText, currentWidth, currentClusters, shaped.ResolvedFont));
                currentText.Clear();
                currentClusters.Clear();
                currentWidth = 0;
            }

            if (currentWidth > 0 && word.PrecedingSpaceWidth > 0)
            {
                AppendSpace(shaped, word.Start, currentText, currentClusters);
                currentWidth += word.PrecedingSpaceWidth;
            }

            AppendRange(shaped, word.Start, word.End, currentText, currentClusters);
            currentWidth += wordWidth;
        }

        lines.Add(Commit(currentText, currentWidth, currentClusters, shaped.ResolvedFont));
        return lines.ToImmutable();

        static ShapedLine Commit(
            StringBuilder text,
            double width,
            ImmutableArray<GlyphCluster>.Builder clusters,
            FontKey font) =>
            new(text.ToString(), width, font, clusters.ToImmutable());
    }

    private static ImmutableArray<WordBoundary> FindWordBoundaries(ShapedText shaped)
    {
        var boundaries = ImmutableArray.CreateBuilder<WordBoundary>();
        var text = shaped.Text;
        var index = 0;
        var previousEnd = 0;

        while (index < text.Length)
        {
            while (index < text.Length && text[index] == ' ')
            {
                index++;
            }

            var wordStart = index;

            while (index < text.Length && text[index] != ' ')
            {
                index++;
            }

            if (wordStart == index)
            {
                break;
            }

            var precedingSpace = text.AsSpan(previousEnd, wordStart - previousEnd);
            var spaceWidth = precedingSpace.Length == 0
                ? 0
                : SumAdvance(shaped.Clusters, previousEnd, wordStart);

            boundaries.Add(new WordBoundary(wordStart, index, spaceWidth));
            previousEnd = index;
        }

        if (boundaries.Count == 0 && text.Length > 0)
        {
            boundaries.Add(new WordBoundary(0, text.Length, 0));
        }

        return boundaries.ToImmutable();
    }

    private static double SumAdvance(
        ImmutableArray<GlyphCluster> clusters,
        int start,
        int end)
    {
        var total = 0d;

        foreach (var cluster in clusters)
        {
            if (cluster.TextStart >= start && cluster.TextStart < end)
            {
                total += cluster.Advance;
            }
        }

        return total;
    }

    private static void AppendRange(
        ShapedText shaped,
        int start,
        int end,
        StringBuilder text,
        ImmutableArray<GlyphCluster>.Builder clusters)
    {
        text.Append(shaped.Text, start, end - start);

        foreach (var cluster in shaped.Clusters)
        {
            if (cluster.TextStart >= start && cluster.TextStart < end)
            {
                clusters.Add(cluster);
            }
        }
    }

    private static void AppendSpace(
        ShapedText shaped,
        int wordStart,
        StringBuilder text,
        ImmutableArray<GlyphCluster>.Builder clusters)
    {
        text.Append(' ');
        clusters.Add(new GlyphCluster(
            wordStart - 1,
            1,
            SumAdvance(shaped.Clusters, wordStart - 1, wordStart)));
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
        double AdvanceWidthPoints,
        FontKey ResolvedFont,
        ImmutableArray<GlyphCluster> Clusters);

    private readonly record struct WordBoundary(
        int Start,
        int End,
        double PrecedingSpaceWidth);
}
