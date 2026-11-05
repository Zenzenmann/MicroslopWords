using System.Collections.Immutable;

namespace MicroslopWords.Layout;

public sealed record LineLayout(
    string Text,
    LayoutRect Bounds,
    double FontSizePoints,
    ImmutableArray<GlyphCluster> Clusters,
    FontKey ResolvedFont);

public sealed record ParagraphLayout(
    Guid ParagraphId,
    LayoutRect Bounds,
    ImmutableArray<LineLayout> Lines);

public sealed record PageLayout(
    int PageNumber,
    LayoutRect Bounds,
    ImmutableArray<ParagraphLayout> Paragraphs);

public sealed record DocumentLayout(
    ImmutableArray<PageLayout> Pages,
    double PageWidthPoints,
    double PageHeightPoints,
    ImmutableArray<Core.Diagnostics.Diagnostic> Diagnostics);
