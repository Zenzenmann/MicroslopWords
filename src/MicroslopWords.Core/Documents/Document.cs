using System.Collections.Immutable;
using MicroslopWords.Core.Styles;

namespace MicroslopWords.Core.Documents;

public sealed record Document(
    Guid Id,
    string Title,
    ImmutableArray<Section> Sections,
    StyleSheet? Styles = null)
{
    public static Document CreateEmpty(string title = "Untitled") =>
        new(
            Guid.NewGuid(),
            title,
            ImmutableArray.Create(Section.CreateEmpty()),
            StyleSheet.Default);

    public StyleSheet EffectiveStyles => Styles ?? StyleSheet.Default;

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

    public Document InsertBlockAfter(Guid afterBlockId, Block block)
    {
        ArgumentNullException.ThrowIfNull(block);

        var sections = Sections
            .Select(section =>
            {
                var index = IndexOfBlock(section.Blocks, afterBlockId);

                if (index < 0)
                {
                    return section;
                }

                return section with { Blocks = section.Blocks.Insert(index + 1, block) };
            })
            .ToImmutableArray();

        return this with { Sections = sections };
    }

    public Document RemoveBlock(Guid blockId)
    {
        var sections = Sections
            .Select(section =>
            {
                var index = IndexOfBlock(section.Blocks, blockId);

                if (index < 0)
                {
                    return section;
                }

                return section with { Blocks = section.Blocks.RemoveAt(index) };
            })
            .ToImmutableArray();

        return this with { Sections = sections };
    }

    public Document ReplaceSection(Section replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        var sections = Sections
            .Select(section => section.Id == replacement.Id ? replacement : section)
            .ToImmutableArray();

        return this with { Sections = sections };
    }

    private static int IndexOfBlock(ImmutableArray<Block> blocks, Guid blockId)
    {
        for (var index = 0; index < blocks.Length; index++)
        {
            if (blocks[index].Id == blockId)
            {
                return index;
            }
        }

        return -1;
    }

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
