using System.Collections.Immutable;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;

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
                    var paragraphBottom = paragraphLines[^1].Bounds.Bottom;

                    paragraphs.Add(new ParagraphLayout(
                        paragraph.Id,
                        new LayoutRect(
                            activePage.MarginLeftPoints,
                            paragraphTop,
                            maxLineWidth,
                            paragraphBottom - paragraphTop),
                        paragraphLines.ToImmutable()));

                    paragraphLines.Clear();
                    maxLineWidth = 0;
                }

                var segments = SplitSoftBreakSegments(paragraph.PlainText);
                var defaultProperties = paragraph.Runs.Length == 0
                    ? RunProperties.Default
                    : paragraph.Runs[0].EffectiveProperties;
                var defaultFont = ResolveFontRequest(defaultProperties);

                for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
                {
                    var segment = segments[segmentIndex];
                    var shaped = ShapeSegment(
                        paragraph,
                        segment.Start,
                        segment.Text.Length,
                        shaper,
                        diagnostics,
                        defaultFont,
                        defaultProperties.FontSizePoints);
                    var lines = WrapStyledText(
                        segment.Text,
                        segment.Start,
                        shaped.Clusters,
                        contentWidth,
                        shaped.DefaultFont,
                        shaped.DefaultFontSize);

                    for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                    {
                        var line = lines[lineIndex];
                        var lineHeight = line.FontSizePoints
                            * paragraph.EffectiveProperties.LineSpacing;

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
                            new LayoutRect(
                                activePage.MarginLeftPoints,
                                cursorY,
                                line.AdvanceWidthPoints,
                                lineHeight),
                            line.FontSizePoints,
                            line.Clusters,
                            line.ResolvedFont,
                            line.Spans));
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

    private static ShapedSegment ShapeSegment(
        ParagraphBlock paragraph,
        int segmentStart,
        int segmentLength,
        TextShaper shaper,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        FontKey defaultFont,
        double defaultFontSize)
    {
        var segmentEnd = segmentStart + segmentLength;
        var clusters = ImmutableArray.CreateBuilder<StyledCluster>();
        var runOffset = 0;

        foreach (var run in paragraph.Runs)
        {
            var runStart = runOffset;
            var runEnd = runStart + run.Text.Length;
            runOffset = runEnd;

            var from = Math.Max(segmentStart, runStart);
            var to = Math.Min(segmentEnd, runEnd);

            if (to <= from)
            {
                continue;
            }

            var properties = run.EffectiveProperties;
            var font = ResolveFontRequest(properties);
            var text = run.Text[(from - runStart)..(to - runStart)];
            var shaped = shaper.Shape(text, font, properties.FontSizePoints);

            foreach (var diagnostic in shaped.Diagnostics)
            {
                diagnostics.Add(diagnostic);
            }

            var displayFont = new FontKey(
                shaped.ResolvedFont.Family,
                font.Weight,
                font.Slant);

            foreach (var cluster in shaped.Clusters)
            {
                clusters.Add(new StyledCluster(
                    from + cluster.TextStart,
                    cluster.TextLength,
                    cluster.Advance,
                    displayFont,
                    properties));
            }
        }

        return new ShapedSegment(
            clusters.ToImmutable(),
            defaultFont,
            defaultFontSize);
    }

    private static ImmutableArray<StyledLine> WrapStyledText(
        string segmentText,
        int segmentStart,
        ImmutableArray<StyledCluster> clusters,
        double maxWidth,
        FontKey defaultFont,
        double defaultFontSize)
    {
        if (segmentText.Length == 0 || clusters.Length == 0)
        {
            return
            [
                new StyledLine(
                    string.Empty,
                    segmentStart,
                    0,
                    defaultFontSize,
                    defaultFont,
                    ImmutableArray<GlyphCluster>.Empty,
                    ImmutableArray<LineSpan>.Empty),
            ];
        }

        var lines = ImmutableArray.CreateBuilder<StyledLine>();
        var lineStartCluster = 0;
        var currentWidth = 0d;
        var lastBreak = -1;

        for (var index = 0; index < clusters.Length; index++)
        {
            currentWidth += clusters[index].Advance;

            if (IsBreakOpportunity(segmentText, segmentStart, clusters[index]))
            {
                lastBreak = index;
            }

            if (currentWidth <= maxWidth || index <= lineStartCluster)
            {
                continue;
            }

            var breakAt = lastBreak >= lineStartCluster ? lastBreak : index - 1;
            lines.Add(CommitLine(
                segmentText,
                segmentStart,
                clusters,
                lineStartCluster,
                breakAt));

            lineStartCluster = breakAt + 1;
            currentWidth = 0;
            lastBreak = -1;

            for (var replay = lineStartCluster; replay <= index; replay++)
            {
                currentWidth += clusters[replay].Advance;

                if (IsBreakOpportunity(segmentText, segmentStart, clusters[replay]))
                {
                    lastBreak = replay;
                }
            }
        }

        lines.Add(CommitLine(
            segmentText,
            segmentStart,
            clusters,
            lineStartCluster,
            clusters.Length - 1));

        return lines.ToImmutable();
    }

    private static StyledLine CommitLine(
        string segmentText,
        int segmentStart,
        ImmutableArray<StyledCluster> clusters,
        int startCluster,
        int endCluster)
    {
        var first = clusters[startCluster];
        var last = clusters[endCluster];
        var lineStart = first.TextStart;
        var lineEnd = last.TextStart + last.TextLength;
        var width = 0d;
        var maxFontSize = 0d;
        var builtClusters = ImmutableArray.CreateBuilder<GlyphCluster>(
            endCluster - startCluster + 1);
        var spans = ImmutableArray.CreateBuilder<LineSpan>();
        var spanStartCluster = startCluster;
        var spanX = 0d;

        for (var index = startCluster; index <= endCluster; index++)
        {
            width += clusters[index].Advance;
            maxFontSize = Math.Max(maxFontSize, clusters[index].Properties.FontSizePoints);
            builtClusters.Add(new GlyphCluster(
                clusters[index].TextStart,
                clusters[index].TextLength,
                clusters[index].Advance));
        }

        while (spanStartCluster <= endCluster)
        {
            var spanEndCluster = spanStartCluster;
            var spanWidth = clusters[spanStartCluster].Advance;

            while (spanEndCluster + 1 <= endCluster
                && SameStyle(clusters[spanEndCluster], clusters[spanEndCluster + 1]))
            {
                spanEndCluster++;
                spanWidth += clusters[spanEndCluster].Advance;
            }

            var spanStart = clusters[spanStartCluster].TextStart;
            var spanEnd = clusters[spanEndCluster].TextStart
                + clusters[spanEndCluster].TextLength;
            var properties = clusters[spanStartCluster].Properties;
            spans.Add(new LineSpan(
                spanStart - lineStart,
                spanEnd - spanStart,
                spanX,
                spanWidth,
                clusters[spanStartCluster].Font,
                properties.FontSizePoints,
                properties.ColorHex,
                properties.HighlightHex,
                properties.Underline,
                properties.Strikethrough));

            spanX += spanWidth;
            spanStartCluster = spanEndCluster + 1;
        }

        return new StyledLine(
            segmentText.Substring(lineStart - segmentStart, lineEnd - lineStart),
            lineStart,
            width,
            maxFontSize <= 0 ? clusters[startCluster].Properties.FontSizePoints : maxFontSize,
            clusters[startCluster].Font,
            builtClusters.ToImmutable(),
            spans.ToImmutable());
    }

    private static bool IsBreakOpportunity(
        string segmentText,
        int segmentStart,
        StyledCluster cluster)
    {
        var index = cluster.TextStart - segmentStart;

        return index >= 0
            && index < segmentText.Length
            && char.IsWhiteSpace(segmentText[index]);
    }

    private static bool SameStyle(StyledCluster left, StyledCluster right) =>
        left.Font == right.Font
        && left.Properties.FontSizePoints == right.Properties.FontSizePoints
        && string.Equals(left.Properties.ColorHex, right.Properties.ColorHex, StringComparison.Ordinal)
        && string.Equals(left.Properties.HighlightHex, right.Properties.HighlightHex, StringComparison.Ordinal)
        && left.Properties.Underline == right.Properties.Underline
        && left.Properties.Strikethrough == right.Properties.Strikethrough;

    private static FontKey ResolveFontRequest(RunProperties properties) =>
        new(
            properties.FontFamily,
            properties.Bold ? FontWeight.Bold : FontWeight.Regular,
            properties.Italic ? FontSlant.Italic : FontSlant.Upright);

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

    private readonly record struct StyledCluster(
        int TextStart,
        int TextLength,
        double Advance,
        FontKey Font,
        RunProperties Properties);

    private readonly record struct ShapedSegment(
        ImmutableArray<StyledCluster> Clusters,
        FontKey DefaultFont,
        double DefaultFontSize);

    private readonly record struct StyledLine(
        string Text,
        int TextStart,
        double AdvanceWidthPoints,
        double FontSizePoints,
        FontKey ResolvedFont,
        ImmutableArray<GlyphCluster> Clusters,
        ImmutableArray<LineSpan> Spans);
}
