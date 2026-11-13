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

[Flags]
public enum ParagraphPropertyOverrides
{
    None = 0,
    Alignment = 1 << 0,
    SpaceBefore = 1 << 1,
    SpaceAfter = 1 << 2,
    LineSpacing = 1 << 3,
    LeftIndent = 1 << 4,
    RightIndent = 1 << 5,
    FirstLineIndent = 1 << 6,
    List = 1 << 7,
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

    public ParagraphPropertyOverrides Overrides { get; init; }

    public static ParagraphProperties Default { get; } = new();
}
