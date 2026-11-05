namespace MicroslopWords.Core.Documents;

public enum ParagraphAlignment
{
    Left,
    Center,
    Right,
    Justify,
}

public enum ListKind
{
    None,
    Bulleted,
    Numbered,
}

public sealed record ParagraphProperties
{
    public ParagraphAlignment Alignment { get; init; } = ParagraphAlignment.Left;

    public double SpaceBeforePoints { get; init; }

    public double SpaceAfterPoints { get; init; } = 8;

    public double LineSpacing { get; init; } = 1.15;

    public double LeftIndentPoints { get; init; }

    public double RightIndentPoints { get; init; }

    public double FirstLineIndentPoints { get; init; }

    public ListKind ListKind { get; init; } = ListKind.None;

    public int ListLevel { get; init; } = 1;

    public Guid? ListId { get; init; }

    public string? StyleId { get; init; }

    public static ParagraphProperties Default { get; } = new();
}
