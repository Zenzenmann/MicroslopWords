using System.Collections.Immutable;

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

public sealed record LineLayout(
    string Text,
    int TextStart,
    bool IsAfterSoftBreak,
    LayoutRect Bounds,
    double FontSizePoints,
    ImmutableArray<GlyphCluster> Clusters,
    FontKey ResolvedFont,
    ImmutableArray<LineSpan> Spans);

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

public sealed record PageLayout(
    int PageNumber,
    LayoutRect Bounds,
    ImmutableArray<ParagraphLayout> Paragraphs);

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
