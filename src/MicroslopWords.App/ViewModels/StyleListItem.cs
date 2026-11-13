using MicroslopWords.Core.Styles;

namespace MicroslopWords.App.ViewModels;

public sealed record StyleListItem(
    string Id,
    string Name,
    string Kind,
    double PreviewFontSize,
    bool PreviewBold,
    bool PreviewItalic,
    bool IsCurrent)
{
    public string PreviewDescription =>
        $"{Kind} · {PreviewFontSize:0.#} pt"
        + (PreviewBold ? " · Bold" : string.Empty)
        + (PreviewItalic ? " · Italic" : string.Empty);

    public double PreviewDisplaySize => Math.Clamp(PreviewFontSize, 10, 18);

    public string PreviewWeight => PreviewBold ? "Bold" : "Normal";

    public string PreviewStyle => PreviewItalic ? "Italic" : "Normal";

    public static StyleListItem FromParagraphStyle(
        ParagraphStyle style,
        bool isCurrent)
    {
        var preview = style.CharacterProperties ?? Core.Documents.RunProperties.Default;

        return new StyleListItem(
            style.Id,
            style.Name,
            "Paragraph",
            preview.FontSizePoints,
            preview.Bold,
            preview.Italic,
            isCurrent);
    }

    public static StyleListItem FromCharacterStyle(
        CharacterStyle style,
        bool isCurrent) =>
        new(
            style.Id,
            style.Name,
            "Character",
            style.Properties.FontSizePoints,
            style.Properties.Bold,
            style.Properties.Italic,
            isCurrent);
}
