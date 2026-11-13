using System.Collections.Immutable;
using MicroslopWords.Core.Documents;

namespace MicroslopWords.Core.Styles;

public sealed record ParagraphStyle(
    string Id,
    string Name,
    ParagraphProperties Properties,
    RunProperties? CharacterProperties = null);

public sealed record CharacterStyle(string Id, string Name, RunProperties Properties);

public sealed record StyleSheet(
    ImmutableArray<ParagraphStyle> ParagraphStyles,
    ImmutableArray<CharacterStyle> CharacterStyles)
{
    public const string NormalStyleId = "Normal";
    public const string DefaultCharacterStyleId = "Default";

    public static StyleSheet Default { get; } = CreateDefault();

    public static StyleSheet CreateDefault() =>
        new(
            ImmutableArray.Create(
                new ParagraphStyle(
                    NormalStyleId,
                    "Normal",
                    ParagraphProperties.Default,
                    RunProperties.Default),
                new ParagraphStyle(
                    "Title",
                    "Title",
                    ParagraphProperties.Default with
                    {
                        Alignment = ParagraphAlignment.Center,
                        SpaceAfterPoints = 12,
                        LineSpacing = 1,
                    },
                    RunProperties.Default with { FontSizePoints = 28, Bold = true }),
                Heading("Heading1", "Heading 1", 18, 8, 18),
                Heading("Heading2", "Heading 2", 14, 6, 14),
                Heading("Heading3", "Heading 3", 12, 4, 12)),
            ImmutableArray.Create(
                new CharacterStyle(DefaultCharacterStyleId, "Default", RunProperties.Default),
                new CharacterStyle(
                    "Emphasis",
                    "Emphasis",
                    RunProperties.Default with { Italic = true }),
                new CharacterStyle(
                    "Strong",
                    "Strong",
                    RunProperties.Default with { Bold = true })));

    public ParagraphStyle? FindParagraphStyle(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : ParagraphStyles.FirstOrDefault(style =>
                string.Equals(style.Id, id, StringComparison.Ordinal));

    public CharacterStyle? FindCharacterStyle(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : CharacterStyles.FirstOrDefault(style =>
                string.Equals(style.Id, id, StringComparison.Ordinal));

    public bool HasStyleName(string name, string? exceptId = null)
    {
        var normalized = name.Trim();

        return ParagraphStyles.Any(style =>
                !string.Equals(style.Id, exceptId, StringComparison.Ordinal)
                && string.Equals(style.Name, normalized, StringComparison.OrdinalIgnoreCase))
            || CharacterStyles.Any(style =>
                !string.Equals(style.Id, exceptId, StringComparison.Ordinal)
                && string.Equals(style.Name, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static ParagraphStyle Heading(
        string id,
        string name,
        double fontSizePoints,
        double spaceAfterPoints,
        double spaceBeforePoints) =>
        new(
            id,
            name,
            ParagraphProperties.Default with
            {
                SpaceBeforePoints = spaceBeforePoints,
                SpaceAfterPoints = spaceAfterPoints,
            },
            RunProperties.Default with { FontSizePoints = fontSizePoints, Bold = true });
}
