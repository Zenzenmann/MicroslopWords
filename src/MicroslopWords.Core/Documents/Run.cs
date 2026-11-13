namespace MicroslopWords.Core.Documents;

public sealed record Run(
    Guid Id,
    string Text,
    RunProperties? Properties = null,
    InlineImage? Image = null)
{
    public static Run Create(string text, RunProperties? properties = null) =>
        new(Guid.NewGuid(), text, properties);

    public static Run CreateImage(
        InlineImage image,
        RunProperties? properties = null)
    {
        ArgumentNullException.ThrowIfNull(image);

        return new Run(
            Guid.NewGuid(),
            InlineImage.PlaceholderCharacter.ToString(),
            properties,
            image);
    }

    public RunProperties EffectiveProperties => Properties ?? RunProperties.Default;

    // An image only occupies its single object-replacement character. If an
    // edit slices that character away, the metadata must stop rendering too.
    public InlineImage? EffectiveImage =>
        Image is not null
        && Text.Length == 1
        && Text[0] == InlineImage.PlaceholderCharacter
            ? Image
            : null;

    public bool IsImage => EffectiveImage is not null;
}
