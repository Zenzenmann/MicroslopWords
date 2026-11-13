using System.Collections.Immutable;
using MicroslopWords.Core.Documents;

namespace MicroslopWords.Layout;

public sealed record LineSpan(
    int TextStart,
    int TextLength,
    double X,
    double Width,
    FontKey Font,
    double FontSizePoints,
    string ColorHex,
    string? HighlightHex,
    bool Underline,
    bool Strikethrough);

public sealed record InlineImageLayout(
    Guid RunId,
    int TextOffset,
    LayoutRect Bounds,
    InlineImage Image);

public sealed record LineLayout(
    string Text,
    int TextStart,
    bool IsAfterSoftBreak,
    LayoutRect Bounds,
    double FontSizePoints,
    ImmutableArray<GlyphCluster> Clusters,
    FontKey ResolvedFont,
    ImmutableArray<LineSpan> Spans,
    ImmutableArray<InlineImageLayout> Images);

public sealed record ParagraphLayout(
    Guid ParagraphId,
    LayoutRect Bounds,
    ImmutableArray<LineLayout> Lines,
    string? ListLabel = null,
    double ListLabelX = 0);

public sealed record PageBreakLayout(
    int PageNumber,
    double Y,
    Guid ParagraphId,
    int TextOffset);

public sealed record TableCellLayout(
    Guid CellId,
    Guid TableId,
    int RowIndex,
    int ColumnIndex,
    int ColumnSpan,
    int RowSpan,
    LayoutRect Bounds,
    ImmutableArray<ParagraphLayout> Paragraphs,
    bool IsHeader,
    string? ShadingHex,
    Core.Documents.TableBorders? Borders);

public sealed record TableRowLayout(
    int RowIndex,
    LayoutRect Bounds,
    ImmutableArray<TableCellLayout> Cells,
    bool IsHeader);

public sealed record TableLayout(
    Guid TableId,
    LayoutRect Bounds,
    ImmutableArray<TableRowLayout> Rows,
    bool ContinuesFromPreviousPage);

public sealed record HeaderFooterLayout(
    HeaderFooterKind Kind,
    HeaderFooterVariant Variant,
    Guid SectionId,
    LayoutRect Bounds,
    ImmutableArray<ParagraphLayout> Paragraphs);

public sealed record PageLayout(
    int PageNumber,
    LayoutRect Bounds,
    ImmutableArray<ParagraphLayout> Paragraphs,
    ImmutableArray<TableLayout> Tables,
    int SectionIndex = 0,
    bool IsSectionFirstPage = true,
    HeaderFooterLayout? Header = null,
    HeaderFooterLayout? Footer = null,
    double MarginTopPoints = 72,
    double MarginBottomPoints = 72,
    double MarginLeftPoints = 72,
    double MarginRightPoints = 72);

public sealed record DocumentLayout(
    ImmutableArray<PageLayout> Pages,
    double PageWidthPoints,
    double PageHeightPoints,
    ImmutableArray<PageBreakLayout> PageBreaks,
    ImmutableArray<Core.Diagnostics.Diagnostic> Diagnostics);

public sealed record CaretLayout(
    PageLayout Page,
    int LineIndex,
    LineLayout Line,
    LayoutRect Bounds);
