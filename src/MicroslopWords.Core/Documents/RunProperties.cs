namespace MicroslopWords.Core.Documents;

[Flags]
public enum RunPropertyOverrides
{
    None = 0,
    FontFamily = 1 << 0,
    FontSize = 1 << 1,
    Bold = 1 << 2,
    Italic = 1 << 3,
    Underline = 1 << 4,
    Strikethrough = 1 << 5,
    Color = 1 << 6,
    Highlight = 1 << 7,
}

public sealed record RunProperties
{
    public string FontFamily { get; init; } = "Arial";

    public double FontSizePoints { get; init; } = 11;

    public bool Bold { get; init; }

    public bool Italic { get; init; }

    public bool Underline { get; init; }

    public bool Strikethrough { get; init; }

    public string ColorHex { get; init; } = "#000000";

    public string? HighlightHex { get; init; }

    public string? StyleId { get; init; }

    public RunPropertyOverrides Overrides { get; init; }

    public static RunProperties Default { get; } = new();
}
