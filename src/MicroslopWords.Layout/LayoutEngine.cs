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
        var tables = ImmutableArray.CreateBuilder<TableLayout>();
        var pageNumber = 1;
        var cursorY = activePage.MarginTopPoints;
        var currentSectionIndex = 0;
        var sectionFirstPageNumber = 1;

        void FlushPage()
        {
            if (paragraphs.Count == 0 && tables.Count == 0)
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
                paragraphs.ToImmutable(),
                tables.ToImmutable(),
                currentSectionIndex,
                pageNumber == sectionFirstPageNumber,
                null,
                null,
                activePage.MarginTopPoints,
                activePage.MarginBottomPoints,
                activePage.MarginLeftPoints,
                activePage.MarginRightPoints));

            paragraphs.Clear();
            tables.Clear();
            pageNumber++;
            cursorY = activePage.MarginTopPoints;
        }

        void LayoutTable(TableBlock table)
        {
            var totalWidth = table.ColumnWidths.Sum();
            var targetWidth = Math.Min(
                table.WidthPoints ?? totalWidth,
                contentWidth);
            targetWidth = Math.Max(36, targetWidth);
            var scale = totalWidth > 0 ? targetWidth / totalWidth : 1;
            var widths = table.ColumnWidths
                .Select(width => Math.Max(18, width * scale))
                .ToArray();

            if (widths.Sum() > contentWidth)
            {
                var correction = contentWidth / widths.Sum();
                widths = widths.Select(width => width * correction).ToArray();
            }

            var left = activePage.MarginLeftPoints;
            var measuredRows = new TableRowLayout[table.Rows.Length];
            var rowHeights = new double[table.Rows.Length];

            for (var index = 0; index < table.Rows.Length; index++)
            {
                var measured = MeasureTableRow(
                    table,
                    table.Rows[index],
                    index,
                    widths,
                    left,
                    shaper,
                    styleSheet,
                    diagnostics,
                    listLabels,
                    contentHeight);
                measuredRows[index] = measured;
                rowHeights[index] = measured.Bounds.Height;
            }

            for (var index = 0; index < table.Rows.Length; index++)
            {
                foreach (var cell in table.Rows[index].Cells)
                {
                    if (cell.IsMergedContinuation || cell.RowSpan <= 1)
                    {
                        continue;
                    }

                    var required = measuredRows[index].Cells
                        .FirstOrDefault(item => item.CellId == cell.Id)?.Bounds.Height ?? 0;
                    var span = Math.Min(cell.RowSpan, rowHeights.Length - index);
                    var available = 0d;

                    for (var offset = 0; offset < span; offset++)
                    {
                        available += rowHeights[index + offset];
                    }

                    if (required > available && span > 0)
                    {
                        rowHeights[index + span - 1] += required - available;
                    }
                }
            }

            TableRowLayout? headerLayout = null;

            if (table.HasHeaderRow && measuredRows.Length > 0)
            {
                headerLayout = measuredRows[0];
            }

            var segmentRows = new List<TableRowLayout>();
            var continues = false;

            void AddSegment()
            {
                if (segmentRows.Count == 0)
                {
                    return;
                }

                var top = segmentRows[0].Bounds.Y;
                var bottom = segmentRows[^1].Bounds.Bottom;

                tables.Add(new TableLayout(
                    table.Id,
                    new LayoutRect(
                        left,
                        top,
                        widths.Sum(),
                        bottom - top),
                    segmentRows.ToImmutableArray(),
                    continues));

                segmentRows.Clear();
            }

            for (var index = 0; index < table.Rows.Length; index++)
            {
                var height = Math.Max(18, rowHeights[index]);

                if (cursorY + height > pageBottom
                    && !RowIsInsideVerticalMerge(table, index)
                    && (segmentRows.Count > 0 || paragraphs.Count > 0 || tables.Count > 0))
                {
                    AddSegment();
                    FlushPage();
                    continues = true;

                    if (index > 0 && headerLayout is not null)
                    {
                        var placedHeader = OffsetTableRow(
                            headerLayout,
                            new LayoutRect(
                                left,
                                cursorY,
                                widths.Sum(),
                                Math.Max(18, rowHeights[0])),
                            rowHeights);
                        segmentRows.Add(placedHeader);
                        cursorY += Math.Max(18, rowHeights[0]);
                    }
                }

                if (cursorY + height > pageBottom
                    && segmentRows.Count == 0
                    && paragraphs.Count == 0
                    && tables.Count == 0)
                {
                    diagnostics.Add(new Diagnostic(
                        LayoutDiagnosticCodes.TableRowOverflow,
                        DiagnosticSeverity.Warning,
                        "A table row is taller than the page and may be clipped."));
                }

                var row = OffsetTableRow(
                    measuredRows[index],
                    new LayoutRect(
                        left,
                        cursorY,
                        widths.Sum(),
                        height),
                    rowHeights);
                segmentRows.Add(row);
                cursorY += height;
            }

            AddSegment();

            if (cursorY + 6 > pageBottom)
            {
                FlushPage();
            }
            else
            {
                cursorY += 6;
            }
        }

        for (var sectionIndex = 0; sectionIndex < document.Sections.Length; sectionIndex++)
        {
            var section = document.Sections[sectionIndex];

            if (paragraphs.Count > 0 || tables.Count > 0)
            {
                FlushPage();
            }

            currentSectionIndex = sectionIndex;
            sectionFirstPageNumber = pageNumber;
            activePage = pageSettings ?? section.EffectivePage;
            contentWidth = constraints?.MaxWidthPoints ?? activePage.ContentWidthPoints;
            contentHeight = constraints?.MaxHeightPoints ?? activePage.ContentHeightPoints;
            pageBottom = activePage.MarginTopPoints + contentHeight;
            cursorY = activePage.MarginTopPoints;

            foreach (var block in section.Blocks)
            {
                if (block is TableBlock table)
                {
                    LayoutTable(table);
                    continue;
                }

                if (block is not ParagraphBlock paragraph)
                {
                    continue;
                }

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
                        styleSheet,
                        wrapWidth,
                        contentHeight);
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
                        var lineHeight = Math.Max(
                            line.FontSizePoints * properties.LineSpacing,
                            line.ImageHeightPoints);

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

                        var images = ImmutableArray.CreateBuilder<InlineImageLayout>(
                            line.Images.Length);

                        foreach (var image in line.Images)
                        {
                            images.Add(new InlineImageLayout(
                                image.RunId,
                                image.TextStart,
                                new LayoutRect(
                                    lineX + image.X,
                                    cursorY + lineHeight - image.HeightPoints,
                                    image.WidthPoints,
                                    image.HeightPoints),
                                image.Image));
                        }

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
                            line.Spans,
                            images.ToImmutable()));
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
                ImmutableArray<ParagraphLayout>.Empty,
                ImmutableArray<TableLayout>.Empty,
                0,
                true,
                null,
                null,
                defaultPage.MarginTopPoints,
                defaultPage.MarginBottomPoints,
                defaultPage.MarginLeftPoints,
                defaultPage.MarginRightPoints));
        }

        ApplyHeadersFooters(
            document,
            pages,
            shaper,
            styleSheet,
            diagnostics);

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

    private const double HeaderFooterDistancePoints = 36;
    private const double HeaderFooterGapPoints = 6;

    private static void ApplyHeadersFooters(
        Document document,
        ImmutableArray<PageLayout>.Builder pages,
        TextShaper shaper,
        StyleSheet styleSheet,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        if (pages.Count == 0 || document.Sections.Length == 0)
        {
            return;
        }

        var pageNumbers = ResolvePageNumberTexts(document, pages);
        var totalPages = pages.Count;
        var overflowReported = false;

        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            var page = pages[pageIndex];
            var sectionIndex = Math.Clamp(
                page.SectionIndex,
                0,
                document.Sections.Length - 1);
            var section = document.Sections[sectionIndex];
            var variant = page.IsSectionFirstPage && section.DifferentFirstPage
                ? HeaderFooterVariant.FirstPage
                : HeaderFooterVariant.Default;
            var (pageText, format) = pageNumbers[pageIndex];
            var totalText = PageNumberFormatter.Format(totalPages, format);
            var header = LayoutHeaderFooter(
                document,
                page,
                section,
                HeaderFooterKind.Header,
                variant,
                pageText,
                totalText,
                shaper,
                styleSheet,
                diagnostics,
                ref overflowReported);
            var footer = LayoutHeaderFooter(
                document,
                page,
                section,
                HeaderFooterKind.Footer,
                variant,
                pageText,
                totalText,
                shaper,
                styleSheet,
                diagnostics,
                ref overflowReported);

            pages[pageIndex] = page with
            {
                Header = header,
                Footer = footer,
            };
        }
    }

    private static (string Text, PageNumberFormat Format)[] ResolvePageNumberTexts(
        Document document,
        ImmutableArray<PageLayout>.Builder pages)
    {
        var values = new (string Text, PageNumberFormat Format)[pages.Count];
        var number = 1;
        var format = PageNumberFormat.DecimalNumber;

        for (var index = 0; index < pages.Count; index++)
        {
            var page = pages[index];
            var sectionIndex = Math.Clamp(
                page.SectionIndex,
                0,
                document.Sections.Length - 1);
            var section = document.Sections[sectionIndex];

            if (page.IsSectionFirstPage)
            {
                if (section.PageNumberStart is { } start)
                {
                    number = start;
                }

                if (section.PageNumberFormat is { } sectionFormat)
                {
                    format = sectionFormat;
                }
            }

            values[index] = (PageNumberFormatter.Format(number, format), format);
            number++;
        }

        return values;
    }

    private static HeaderFooterLayout? LayoutHeaderFooter(
        Document document,
        PageLayout page,
        Section section,
        HeaderFooterKind kind,
        HeaderFooterVariant variant,
        string pageText,
        string totalText,
        TextShaper shaper,
        StyleSheet styleSheet,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        ref bool overflowReported)
    {
        var content = document.ResolveHeaderFooter(page.SectionIndex, kind, variant);

        if (content is null || !content.HasVisibleContent)
        {
            return null;
        }

        var width = Math.Max(
            1,
            page.Bounds.Width
                - page.MarginLeftPoints
                - page.MarginRightPoints);
        var left = page.MarginLeftPoints;
        var paragraphs = content.Blocks
            .OfType<ParagraphBlock>()
            .Select(paragraph => ResolvePageFields(paragraph, pageText, totalText))
            .ToImmutableArray();
        var layout = LayoutHeaderFooterParagraphs(
            paragraphs,
            left,
            width,
            shaper,
            styleSheet,
            diagnostics);
        var height = layout.Length == 0
            ? 0
            : layout.Max(paragraph => paragraph.Bounds.Bottom);

        if (height <= 0)
        {
            return null;
        }

        var available = kind == HeaderFooterKind.Header
            ? page.MarginTopPoints - HeaderFooterDistancePoints - HeaderFooterGapPoints
            : page.MarginBottomPoints - HeaderFooterDistancePoints - HeaderFooterGapPoints;

        if (height > available && !overflowReported)
        {
            overflowReported = true;
            diagnostics.Add(new Diagnostic(
                LayoutDiagnosticCodes.HeaderFooterOverflow,
                DiagnosticSeverity.Warning,
                "Header or footer content exceeds the available margin area."));
        }

        var top = kind == HeaderFooterKind.Header
            ? HeaderFooterDistancePoints
            : page.Bounds.Height - HeaderFooterDistancePoints - height;

        return new HeaderFooterLayout(
            kind,
            variant,
            section.Id,
            new LayoutRect(left, top, width, height),
            layout);
    }

    private static ParagraphBlock ResolvePageFields(
        ParagraphBlock paragraph,
        string pageText,
        string totalText) =>
        paragraph with
        {
            Runs = paragraph.Runs
                .Select(run => run.PageField switch
                {
                    PageFieldKind.PageNumber => run with { Text = pageText },
                    PageFieldKind.TotalPages => run with { Text = totalText },
                    _ => run,
                })
                .ToImmutableArray(),
        };

    private static ImmutableArray<ParagraphLayout> LayoutHeaderFooterParagraphs(
        ImmutableArray<ParagraphBlock> paragraphs,
        double left,
        double width,
        TextShaper shaper,
        StyleSheet styleSheet,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var result = ImmutableArray.CreateBuilder<ParagraphLayout>(paragraphs.Length);
        var y = 0d;

        foreach (var paragraph in paragraphs)
        {
            var properties = StyleResolver.ResolveParagraph(paragraph, styleSheet);
            var baseLeft = left + properties.LeftIndentPoints;
            var textLeft = baseLeft;
            var wrapWidth = Math.Max(
                1,
                width
                    - properties.LeftIndentPoints
                    - properties.RightIndentPoints
                    - Math.Max(0, properties.FirstLineIndentPoints));
            var firstLineExtra = properties.FirstLineIndentPoints;
            var defaultProperties = paragraph.Runs.Length == 0
                ? styleSheet.FindParagraphStyle(properties.StyleId)?.CharacterProperties
                    ?? RunProperties.Default
                : StyleResolver.ResolveRun(paragraph.Runs[0], paragraph, styleSheet);
            var defaultFont = ResolveFontRequest(defaultProperties);
            var segments = SplitTextSegments(paragraph.PlainText);
            var lines = ImmutableArray.CreateBuilder<LineLayout>();
            var afterSoftBreak = false;
            y += properties.SpaceBeforePoints;
            var paragraphTop = y;

            foreach (var segment in segments)
            {
                var shaped = ShapeSegment(
                    paragraph,
                    segment.Start,
                    segment.Text.Length,
                    shaper,
                    diagnostics,
                    defaultFont,
                    defaultProperties.FontSizePoints,
                    styleSheet,
                    wrapWidth,
                    double.MaxValue);
                var wrapped = WrapStyledText(
                    segment.Text,
                    segment.Start,
                    shaped.Clusters,
                    wrapWidth,
                    shaped.DefaultFont,
                    shaped.DefaultFontSize,
                    properties.Alignment == ParagraphAlignment.Justify);

                for (var lineIndex = 0; lineIndex < wrapped.Length; lineIndex++)
                {
                    var line = wrapped[lineIndex];
                    var lineHeight = Math.Max(
                        line.FontSizePoints * properties.LineSpacing,
                        line.ImageHeightPoints);
                    var lineX = textLeft;

                    if (lineIndex == 0)
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

                    lineX = Math.Max(left, lineX);

                    var images = ImmutableArray.CreateBuilder<InlineImageLayout>(
                        line.Images.Length);

                    foreach (var image in line.Images)
                    {
                        images.Add(new InlineImageLayout(
                            image.RunId,
                            image.TextStart,
                            new LayoutRect(
                                lineX + image.X,
                                y + lineHeight - image.HeightPoints,
                                image.WidthPoints,
                                image.HeightPoints),
                            image.Image));
                    }

                    lines.Add(new LineLayout(
                        line.Text,
                        line.TextStart,
                        afterSoftBreak && lineIndex == 0,
                        new LayoutRect(
                            lineX,
                            y,
                            line.AdvanceWidthPoints,
                            lineHeight),
                        line.FontSizePoints,
                        line.Clusters,
                        line.ResolvedFont,
                        line.Spans,
                        images.ToImmutable()));
                    y += lineHeight;
                }

                afterSoftBreak = segment.BreakAfter != TextBreakKind.None;
            }

            result.Add(new ParagraphLayout(
                paragraph.Id,
                new LayoutRect(left, paragraphTop, width, y - paragraphTop),
                lines.ToImmutable()));
            y += properties.SpaceAfterPoints;
        }

        return result.ToImmutable();
    }

    private static ShapedSegment ShapeSegment(
        ParagraphBlock paragraph,
        int segmentStart,
        int segmentLength,
        TextShaper shaper,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        FontKey defaultFont,
        double defaultFontSize,
        StyleSheet styleSheet,
        double maxWidth,
        double maxHeight)
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

            if (run.EffectiveImage is { } image)
            {
                // Images are atomic: they are never shaped or split across
                // segments, and their displayed size is capped to the line.
                if (from != runStart || to != runEnd)
                {
                    continue;
                }

                var (width, height) = ImageValidation.FitSize(
                    image.WidthPoints,
                    image.HeightPoints,
                    maxWidth,
                    maxHeight);
                clusters.Add(new StyledCluster(
                    runStart,
                    run.Text.Length,
                    width,
                    font,
                    properties,
                    image,
                    height,
                    run.Id));
                continue;
            }

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
                    properties,
                    RunId: run.Id));
            }
        }

        return new ShapedSegment(
            clusters.ToImmutable(),
            defaultFont,
            defaultFontSize);
    }

    private const double TableCellPaddingPoints = 4;

    private static bool RowIsInsideVerticalMerge(TableBlock table, int rowIndex)
    {
        for (var candidateRow = 0; candidateRow < rowIndex; candidateRow++)
        {
            foreach (var cell in table.Rows[candidateRow].Cells)
            {
                if (!cell.IsMergedContinuation
                    && cell.RowSpan > 1
                    && rowIndex < candidateRow + cell.RowSpan)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static TableRowLayout MeasureTableRow(
        TableBlock table,
        TableRow row,
        int rowIndex,
        double[] widths,
        double left,
        TextShaper shaper,
        StyleSheet styleSheet,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        IReadOnlyDictionary<Guid, string> listLabels,
        double maxHeight)
    {
        var cells = ImmutableArray.CreateBuilder<TableCellLayout>();
        var gridColumn = 0;
        var bottom = 0d;

        foreach (var cell in row.Cells)
        {
            var span = Math.Max(1, cell.ColumnSpan);

            if (!cell.IsMergedContinuation)
            {
                var cellX = left + widths.Take(gridColumn).Sum();
                var cellWidth = widths.Skip(gridColumn).Take(span).Sum();
                var paragraphs = LayoutCellParagraphs(
                    cell,
                    cellX,
                    cellWidth,
                    shaper,
                    styleSheet,
                    diagnostics,
                    listLabels,
                    maxHeight);
                var contentBottom = paragraphs.Length == 0
                    ? 0
                    : paragraphs.Max(item => item.Bounds.Bottom);
                var cellHeight = Math.Max(18, contentBottom + TableCellPaddingPoints);
                cells.Add(new TableCellLayout(
                    cell.Id,
                    table.Id,
                    rowIndex,
                    gridColumn,
                    span,
                    Math.Max(1, cell.RowSpan),
                    new LayoutRect(cellX, 0, cellWidth, cellHeight),
                    paragraphs,
                    row.IsHeader,
                    cell.ShadingHex,
                    cell.Borders));
                bottom = Math.Max(bottom, cellHeight);
            }

            gridColumn += span;
        }

        return new TableRowLayout(
            rowIndex,
            new LayoutRect(left, 0, widths.Sum(), Math.Max(18, bottom)),
            cells.ToImmutable(),
            row.IsHeader);
    }

    private static ImmutableArray<ParagraphLayout> LayoutCellParagraphs(
        TableCell cell,
        double cellX,
        double cellWidth,
        TextShaper shaper,
        StyleSheet styleSheet,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        IReadOnlyDictionary<Guid, string> listLabels,
        double maxHeight)
    {
        var paragraphs = ImmutableArray.CreateBuilder<ParagraphLayout>();
        var y = TableCellPaddingPoints;

        foreach (var paragraph in cell.Paragraphs)
        {
            var properties = StyleResolver.ResolveParagraph(paragraph, styleSheet);
            var isList = properties.ListKind != ListKind.None;
            var listLevel = Math.Clamp(properties.ListLevel, 1, 9);
            var listLabel = isList && listLabels.TryGetValue(paragraph.Id, out var label)
                ? label
                : null;
            var baseLeft = cellX
                + TableCellPaddingPoints
                + properties.LeftIndentPoints
                + (isList ? (listLevel - 1) * ListIndentPoints : 0);
            var textLeft = baseLeft + (isList ? ListIndentPoints : 0);
            var wrapWidth = Math.Max(
                1,
                cellWidth
                    - (TableCellPaddingPoints * 2)
                    - properties.LeftIndentPoints
                    - properties.RightIndentPoints
                    - (isList
                        ? ListIndentPoints + ((listLevel - 1) * ListIndentPoints)
                        : 0)
                    - (isList ? 0 : Math.Max(0, properties.FirstLineIndentPoints)));
            var firstLineExtra = isList ? 0 : properties.FirstLineIndentPoints;
            var defaultProperties = paragraph.Runs.Length == 0
                ? styleSheet.FindParagraphStyle(properties.StyleId)?.CharacterProperties
                    ?? RunProperties.Default
                : StyleResolver.ResolveRun(paragraph.Runs[0], paragraph, styleSheet);
            var defaultFont = ResolveFontRequest(defaultProperties);
            var segments = SplitTextSegments(paragraph.PlainText);
            var lines = ImmutableArray.CreateBuilder<LineLayout>();
            var afterSoftBreak = false;
            y += properties.SpaceBeforePoints;
            var paragraphTop = y;

            foreach (var segment in segments)
            {
                var shaped = ShapeSegment(
                    paragraph,
                    segment.Start,
                    segment.Text.Length,
                    shaper,
                    diagnostics,
                    defaultFont,
                    defaultProperties.FontSizePoints,
                    styleSheet,
                    wrapWidth,
                    maxHeight);
                var wrapped = WrapStyledText(
                    segment.Text,
                    segment.Start,
                    shaped.Clusters,
                    wrapWidth,
                    shaped.DefaultFont,
                    shaped.DefaultFontSize,
                    properties.Alignment == ParagraphAlignment.Justify);

                for (var lineIndex = 0; lineIndex < wrapped.Length; lineIndex++)
                {
                    var line = wrapped[lineIndex];
                    var lineHeight = line.FontSizePoints * properties.LineSpacing;
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

                    lineX = Math.Max(cellX + TableCellPaddingPoints, lineX);

                    var images = ImmutableArray.CreateBuilder<InlineImageLayout>(
                        line.Images.Length);

                    foreach (var image in line.Images)
                    {
                        images.Add(new InlineImageLayout(
                            image.RunId,
                            image.TextStart,
                            new LayoutRect(
                                lineX + image.X,
                                y + lineHeight - image.HeightPoints,
                                image.WidthPoints,
                                image.HeightPoints),
                            image.Image));
                    }

                    lines.Add(new LineLayout(
                        line.Text,
                        line.TextStart,
                        afterSoftBreak && lineIndex == 0,
                        new LayoutRect(
                            lineX,
                            y,
                            line.AdvanceWidthPoints,
                            lineHeight),
                        line.FontSizePoints,
                        line.Clusters,
                        line.ResolvedFont,
                        line.Spans,
                        images.ToImmutable()));
                    y += lineHeight;
                }

                afterSoftBreak = segment.BreakAfter != TextBreakKind.None;
            }

            paragraphs.Add(new ParagraphLayout(
                paragraph.Id,
                new LayoutRect(
                    cellX + TableCellPaddingPoints,
                    paragraphTop,
                    wrapWidth,
                    y - paragraphTop),
                lines.ToImmutable(),
                listLabel,
                cellX + TableCellPaddingPoints));
            y += properties.SpaceAfterPoints;
        }

        return paragraphs.ToImmutable();
    }

    private static TableRowLayout OffsetTableRow(
        TableRowLayout row,
        LayoutRect bounds,
        IReadOnlyList<double> rowHeights)
    {
        var offset = bounds.Y - row.Bounds.Y;

        return row with
        {
            Bounds = bounds with
            {
                X = row.Bounds.X,
                Width = row.Bounds.Width,
            },
            Cells = row.Cells
                .Select(cell =>
                {
                    var height = cell.RowSpan > 1
                        ? rowHeights
                            .Skip(cell.RowIndex)
                            .Take(cell.RowSpan)
                            .Sum()
                        : bounds.Height;

                    return cell with
                    {
                        Bounds = cell.Bounds with
                        {
                            Y = cell.Bounds.Y + offset,
                            Height = height,
                        },
                        Paragraphs = OffsetParagraphLayouts(cell.Paragraphs, offset),
                    };
                })
                .ToImmutableArray(),
        };
    }

    private static ImmutableArray<ParagraphLayout> OffsetParagraphLayouts(
        ImmutableArray<ParagraphLayout> paragraphs,
        double offset) =>
        paragraphs
            .Select(paragraph => paragraph with
            {
                Bounds = paragraph.Bounds with
                {
                    Y = paragraph.Bounds.Y + offset,
                },
                Lines = paragraph.Lines
                    .Select(line => line with
                    {
                        Bounds = line.Bounds with
                        {
                            Y = line.Bounds.Y + offset,
                        },
                    })
                    .ToImmutableArray(),
            })
            .ToImmutableArray();

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
                    ImmutableArray<LineSpan>.Empty,
                    ImmutableArray<StyledImage>.Empty,
                    0),
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
        var images = ImmutableArray.CreateBuilder<StyledImage>();
        var spanStartCluster = startCluster;
        var spanX = 0d;
        var imageX = 0d;
        var maxImageHeight = 0d;

        for (var index = startCluster; index <= endCluster; index++)
        {
            var cluster = clusters[index];
            var advance = advances[index - startCluster];
            builtClusters.Add(new GlyphCluster(
                cluster.TextStart,
                cluster.TextLength,
                advance));

            if (cluster.Image is { } image)
            {
                images.Add(new StyledImage(
                    cluster.RunId,
                    cluster.TextStart,
                    imageX,
                    advance,
                    cluster.ImageHeightPoints,
                    image));
                maxImageHeight = Math.Max(maxImageHeight, cluster.ImageHeightPoints);
            }

            imageX += advance;
        }

        while (spanStartCluster <= endCluster)
        {
            if (clusters[spanStartCluster].Image is not null)
            {
                spanX += advances[spanStartCluster - startCluster];
                spanStartCluster++;
                continue;
            }

            var spanEndCluster = spanStartCluster;
            var spanWidth = advances[spanStartCluster - startCluster];

            while (spanEndCluster + 1 <= endCluster
                && clusters[spanEndCluster + 1].Image is null
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
            spans.ToImmutable(),
            images.ToImmutable(),
            maxImageHeight);
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
        cluster.Image is not null || IsWhitespaceAt(segmentText, segmentStart, cluster);

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

        foreach (var paragraph in document.EnumerateParagraphs())
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
        RunProperties Properties,
        InlineImage? Image = null,
        double ImageHeightPoints = 0,
        Guid RunId = default);

    private readonly record struct ShapedSegment(
        ImmutableArray<StyledCluster> Clusters,
        FontKey DefaultFont,
        double DefaultFontSize);

    private readonly record struct StyledImage(
        Guid RunId,
        int TextStart,
        double X,
        double WidthPoints,
        double HeightPoints,
        InlineImage Image);

    private readonly record struct StyledLine(
        string Text,
        int TextStart,
        double AdvanceWidthPoints,
        double FontSizePoints,
        FontKey ResolvedFont,
        ImmutableArray<GlyphCluster> Clusters,
        ImmutableArray<LineSpan> Spans,
        ImmutableArray<StyledImage> Images,
        double ImageHeightPoints);
}
