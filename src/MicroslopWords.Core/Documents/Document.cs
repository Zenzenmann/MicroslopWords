using System.Collections.Immutable;

namespace MicroslopWords.Core.Documents;

public sealed record Document(
    Guid Id,
    string Title,
    ImmutableArray<Section> Sections)
{
    public static Document CreateEmpty(string title = "Untitled") =>
        new(Guid.NewGuid(), title, ImmutableArray.Create(Section.CreateEmpty()));

    public string PlainText =>
        string.Join(
            Environment.NewLine,
            Sections
                .SelectMany(section => section.Blocks)
                .OfType<ParagraphBlock>()
                .Select(paragraph => paragraph.PlainText));

    public Document ReplaceBlock(Block replacement)
    {
        var sections = Sections
            .Select(section => section.Blocks.Any(block => block.Id == replacement.Id)
                ? section with { Blocks = Replace(section.Blocks, replacement) }
                : section)
            .ToImmutableArray();

        return this with { Sections = sections };
    }

    public ParagraphBlock? FindParagraph(Guid paragraphId) =>
        Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .FirstOrDefault(paragraph => paragraph.Id == paragraphId);

    private static ImmutableArray<Block> Replace(ImmutableArray<Block> blocks, Block replacement)
    {
        var builder = ImmutableArray.CreateBuilder<Block>(blocks.Length);

        foreach (var block in blocks)
        {
            builder.Add(block.Id == replacement.Id ? replacement : block);
        }

        return builder.MoveToImmutable();
    }
}
