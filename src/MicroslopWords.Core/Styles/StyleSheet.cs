using System.Collections.Immutable;
using MicroslopWords.Core.Documents;

namespace MicroslopWords.Core.Styles;

public sealed record ParagraphStyle(string Id, string Name, ParagraphProperties Properties);

public sealed record CharacterStyle(string Id, string Name, RunProperties Properties);

public sealed record StyleSheet(
    ImmutableArray<ParagraphStyle> ParagraphStyles,
    ImmutableArray<CharacterStyle> CharacterStyles)
{
    public static StyleSheet Default { get; } = new(
        ImmutableArray.Create(
            new ParagraphStyle("Normal", "Normal", ParagraphProperties.Default),
            new ParagraphStyle(
                "Heading1",
                "Heading 1",
                ParagraphProperties.Default with { SpaceBeforePoints = 18, SpaceAfterPoints = 8 })),
        ImmutableArray.Create(
            new CharacterStyle("Default", "Default", RunProperties.Default),
            new CharacterStyle(
                "Emphasis",
                "Emphasis",
                RunProperties.Default with { Italic = true })));
}
