namespace MicroslopWords.Core.Documents;

public sealed record RunProperties
{
    public string FontFamily { get; init; } = "Arial";

    public double FontSizePoints { get; init; } = 11;

    public bool Bold { get; init; }

    public bool Italic { get; init; }

    public bool Underline { get; init; }

    public string ColorHex { get; init; } = "#000000";

    public static RunProperties Default { get; } = new();
}
