using System.Collections.Immutable;
using MicroslopWords.Core.Diagnostics;

namespace MicroslopWords.Layout;

public readonly record struct GlyphCluster(
    int TextStart,
    int TextLength,
    double Advance);

public sealed record ShapedText(
    string Text,
    FontKey ResolvedFont,
    double AdvanceWidthPoints,
    ImmutableArray<GlyphCluster> Clusters,
    ImmutableArray<Diagnostic> Diagnostics);
