namespace MicroslopWords.Core.Documents;

public enum ParagraphAlignment
{
    Left,
    Center,
    Right,
    Justify,
}

public sealed record ParagraphProperties
{
    public ParagraphAlignment Alignment { get; init; } = ParagraphAlignment.Left;

    public double SpaceBeforePoints { get; init; }

    public double SpaceAfterPoints { get; init; } = 8;

    public double LineSpacing { get; init; } = 1.15;

    public string? StyleId { get; init; }

    public static ParagraphProperties Default { get; } = new();
}
