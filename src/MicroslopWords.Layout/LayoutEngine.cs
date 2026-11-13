using System.Collections.Immutable;
using System.Globalization;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Styles;

namespace MicroslopWords.Layout;

public sealed class LayoutEngine
{
    private const double ListIndentPoints = 18;

    private static readonly string[] BulletSymbols =
    [
        "\u2022",
        "\u25E6",
        "\u25AA",
        "\u2013",
        "\u00B7",
        "\u25CB",
        "\u25A0",
        "\u25B8",
        "\u2023",
    ];

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
        LayoutConstraints? constraints = null,
        StyleSheet? styles = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var catalog = fontCatalog ?? _fontCatalog;
        var styleSheet = styles ?? document.EffectiveStyles;
        var defaultPage = pageSettings
            ?? document.Sections.FirstOrDefault()?.EffectivePage
            ?? PageSettings.Letter;
        var activePage = defaultPage;
        var contentWidth = constraints?.MaxWidthPoints ?? defaultPage.ContentWidthPoints;
        var contentHeight = constraints?.MaxHeightPoints ?? defaultPage.ContentHeightPoints;
        var pageBottom = activePage.MarginTopPoints + contentHeight;

        using var shaper = new TextShaper(catalog);
        var listLabels = BuildListLabels(document, styleSheet);
        var pageBreaks = ImmutableArray.CreateBuilder<PageBreakLayout>();
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
                var properties = StyleResolver.ResolveParagraph(paragraph, styleSheet);
                var isList = properties.ListKind != ListKind.None;
                var listLevel = Math.Clamp(properties.ListLevel, 1, 9);
                var listLabel = isList && listLabels.TryGetValue(paragraph.Id, out var label)
                    ? label
                    : null;
                var baseLeft = activePage.MarginLeftPoints
                    + properties.LeftIndentPoints
                    + (isList ? (listLevel - 1) * ListIndentPoints : 0);
                var textLeft = baseLeft + (isList ? ListIndentPoints : 0);
                var wrapWidth = Math.Max(
                    1,
                    contentWidth
                        - properties.LeftIndentPoints
                        - properties.RightIndentPoints
                        - (isList
                            ? ListIndentPoints + ((listLevel - 1) * ListIndentPoints)
                            : 0)
                        - (isList ? 0 : Math.Max(0, properties.FirstLineIndentPoints)));
                var firstLineExtra = isList ? 0 : properties.FirstLineIndentPoints;
                var listLabelShown = false;

                cursorY += properties.SpaceBeforePoints;

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
                    var shownLabel = listLabelShown ? null : listLabel;

                    paragraphs.Add(new ParagraphLayout(
                        paragraph.Id,
                        new LayoutRect(
                            baseLeft,
                            paragraphTop,
                            maxLineWidth,
                            paragraphBottom - paragraphTop),
                        paragraphLines.ToImmutable(),
                        shownLabel,
                        baseLeft));

                    listLabelShown = true;
                    paragraphLines.Clear();
                    maxLineWidth = 0;
                }

                var segments = SplitTextSegments(paragraph.PlainText);
                var defaultProperties = paragraph.Runs.Length == 0
                    ? styleSheet.FindParagraphStyle(properties.StyleId)?.CharacterProperties
                        ?? RunProperties.Default
                    : StyleResolver.ResolveRun(paragraph.Runs[0], paragraph, styleSheet);
                var defaultFont = ResolveFontRequest(defaultProperties);
                var afterSoftBreak = false;

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
                        defaultProperties.FontSizePoints,
                        styleSheet);
                    var lines = WrapStyledText(
                        segment.Text,
                        segment.Start,
                        shaped.Clusters,
                        wrapWidth,
                        shaped.DefaultFont,
                        shaped.DefaultFontSize,
                        properties.Alignment == ParagraphAlignment.Justify);

                    for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                    {
                        var line = lines[lineIndex];
                        var lineHeight = line.FontSizePoints * properties.LineSpacing;

                        if (cursorY + lineHeight > pageBottom
                            && (paragraphs.Count > 0 || paragraphLines.Count > 0))
                        {
                            FinishParagraphSegment();
                            FlushPage();
                        }

                        var lineX = textLeft;

                        if (!isList && lineIndex == 0)
                        {
                            lineX += firstLineExtra;
                        }

                        switch (properties.Alignment)
                        {
                            case ParagraphAlignment.Center:
                                lineX += (wrapWidth - line.AdvanceWidthPoints) / 2;
                                break;
                            case ParagraphAlignment.Right:
                                lineX += wrapWidth - line.AdvanceWidthPoints;
                                break;
                        }

                        lineX = Math.Max(activePage.MarginLeftPoints, lineX);
                        maxLineWidth = Math.Max(
                            maxLineWidth,
                            (lineX - baseLeft) + line.AdvanceWidthPoints);

                        paragraphLines.Add(new LineLayout(
                            line.Text,
                            line.TextStart,
                            afterSoftBreak && lineIndex == 0,
                            new LayoutRect(
                                lineX,
                                cursorY,
                                line.AdvanceWidthPoints,
                                lineHeight),
                            line.FontSizePoints,
                            line.Clusters,
                            line.ResolvedFont,
                            line.Spans));
                        cursorY += lineHeight;
                    }

                    if (segment.BreakAfter == TextBreakKind.Page)
                    {
                        pageBreaks.Add(new PageBreakLayout(
                            pageNumber,
                            cursorY,
                            paragraph.Id,
                            segment.Start + segment.Text.Length));
                        FinishParagraphSegment();

                        if (paragraphs.Count > 0)
                        {
                            FlushPage();
                        }

                        afterSoftBreak = false;
                    }
                    else if (segment.BreakAfter == TextBreakKind.Soft)
                    {
                        afterSoftBreak = true;
                    }
                    else
                    {
                        afterSoftBreak = false;
                    }
                }

                FinishParagraphSegment();
                cursorY += properties.SpaceAfterPoints;
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
            pageBreaks.ToImmutable(),
            Deduplicate(diagnostics));
    }

    private static ShapedSegment ShapeSegment(
        ParagraphBlock paragraph,
        int segmentStart,
        int segmentLength,
        TextShaper shaper,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        FontKey defaultFont,
        double defaultFontSize,
        StyleSheet styleSheet)
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

            var properties = StyleResolver.ResolveRun(run, paragraph, styleSheet);
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
        double defaultFontSize,
        bool justify)
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
                breakAt,
                maxWidth,
                justify));

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
            clusters.Length - 1,
            maxWidth,
            false));

        return lines.ToImmutable();
    }

    private static StyledLine CommitLine(
        string segmentText,
        int segmentStart,
        ImmutableArray<StyledCluster> clusters,
        int startCluster,
        int endCluster,
        double maxWidth,
        bool justify)
    {
        var first = clusters[startCluster];
        var last = clusters[endCluster];
        var lineStart = first.TextStart;
        var lineEnd = last.TextStart + last.TextLength;
        var count = endCluster - startCluster + 1;
        var advances = new double[count];
        var width = 0d;
        var maxFontSize = 0d;

        for (var index = 0; index < count; index++)
        {
            advances[index] = clusters[startCluster + index].Advance;
            width += advances[index];
            maxFontSize = Math.Max(
                maxFontSize,
                clusters[startCluster + index].Properties.FontSizePoints);
        }

        if (justify && width < maxWidth)
        {
            var spaces = 0;

            for (var index = startCluster; index <= endCluster; index++)
            {
                if (IsWhitespaceAt(segmentText, segmentStart, clusters[index]))
                {
                    spaces++;
                }
            }

            if (spaces > 0)
            {
                var extra = (maxWidth - width) / spaces;

                for (var index = startCluster; index <= endCluster; index++)
                {
                    if (IsWhitespaceAt(segmentText, segmentStart, clusters[index]))
                    {
                        advances[index - startCluster] += extra;
                    }
                }

                width = maxWidth;
            }
        }

        var builtClusters = ImmutableArray.CreateBuilder<GlyphCluster>(count);
        var spans = ImmutableArray.CreateBuilder<LineSpan>();
        var spanStartCluster = startCluster;
        var spanX = 0d;

        for (var index = startCluster; index <= endCluster; index++)
        {
            builtClusters.Add(new GlyphCluster(
                clusters[index].TextStart,
                clusters[index].TextLength,
                advances[index - startCluster]));
        }

        while (spanStartCluster <= endCluster)
        {
            var spanEndCluster = spanStartCluster;
            var spanWidth = advances[spanStartCluster - startCluster];

            while (spanEndCluster + 1 <= endCluster
                && SameStyle(clusters[spanEndCluster], clusters[spanEndCluster + 1]))
            {
                spanEndCluster++;
                spanWidth += advances[spanEndCluster - startCluster];
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
    private static bool IsWhitespaceAt(
        string segmentText,
        int segmentStart,
        StyledCluster cluster)
    {
        var index = cluster.TextStart - segmentStart;

        return index >= 0
            && index < segmentText.Length
            && char.IsWhiteSpace(segmentText[index]);
    }

    private static bool IsBreakOpportunity(
        string segmentText,
        int segmentStart,
        StyledCluster cluster) =>
        IsWhitespaceAt(segmentText, segmentStart, cluster);

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

    private static Dictionary<Guid, string> BuildListLabels(
        Document document,
        StyleSheet styleSheet)
    {
        var labels = new Dictionary<Guid, string>();
        var counters = new Dictionary<Guid, Dictionary<int, int>>();

        foreach (var paragraph in document.Sections
                     .SelectMany(section => section.Blocks)
                     .OfType<ParagraphBlock>())
        {
            var properties = StyleResolver.ResolveParagraph(paragraph, styleSheet);

            if (properties.ListKind == ListKind.None)
            {
                continue;
            }

            var level = Math.Clamp(properties.ListLevel, 1, 9);

            if (properties.ListKind == ListKind.Bulleted)
            {
                labels[paragraph.Id] = BulletSymbols[level - 1];
                continue;
            }

            var listId = properties.ListId ?? paragraph.Id;

            if (!counters.TryGetValue(listId, out var levels))
            {
                levels = [];
                counters[listId] = levels;
            }

            levels.TryGetValue(level, out var current);
            current++;
            levels[level] = current;

            foreach (var deeper in levels.Keys.Where(key => key > level).ToArray())
            {
                levels.Remove(deeper);
            }

            labels[paragraph.Id] = current.ToString(CultureInfo.InvariantCulture) + ".";
        }

        return labels;
    }

    private static ImmutableArray<TextSegment> SplitTextSegments(string text)
    {
        var segments = ImmutableArray.CreateBuilder<TextSegment>();
        var start = 0;

        for (var index = 0; index < text.Length; index++)
        {
            var kind = text[index] switch
            {
                TextBreaks.SoftBreak => TextBreakKind.Soft,
                TextBreaks.PageBreak => TextBreakKind.Page,
                _ => TextBreakKind.None,
            };

            if (kind == TextBreakKind.None)
            {
                continue;
            }

            segments.Add(new TextSegment(start, text[start..index], kind));
            start = index + 1;
        }

        if (start < text.Length
            || segments.Count == 0
            || segments[^1].BreakAfter != TextBreakKind.Page)
        {
            segments.Add(new TextSegment(start, text[start..], TextBreakKind.None));
        }

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

    private enum TextBreakKind
    {
        None,
        Soft,
        Page,
    }

    private readonly record struct TextSegment(
        int Start,
        string Text,
        TextBreakKind BreakAfter);

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
