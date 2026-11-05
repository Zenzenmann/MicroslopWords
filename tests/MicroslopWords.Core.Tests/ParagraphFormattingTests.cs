using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using Shouldly;

namespace MicroslopWords.Core.Tests;

public sealed class ParagraphFormattingTests
{
    [Fact]
    public void Paragraph_properties_apply_only_to_selected_paragraphs()
    {
        var first = ParagraphBlock.Create("First");
        var second = ParagraphBlock.Create("Second");
        var document = CreateDocument(first, second);
        var selection = new Selection(
            TextNavigation.StartOf(second),
            TextNavigation.EndOf(second));

        var after = new ApplyParagraphPropertiesCommand(
            selection,
            properties => properties with
            {
                Alignment = ParagraphAlignment.Center,
                SpaceBeforePoints = 12,
                LineSpacing = 2,
            }).Apply(document);

        var paragraphs = Paragraphs(after);
        paragraphs[0].EffectiveProperties.Alignment.ShouldBe(ParagraphAlignment.Left);
        paragraphs[1].EffectiveProperties.Alignment.ShouldBe(ParagraphAlignment.Center);
        paragraphs[1].EffectiveProperties.SpaceBeforePoints.ShouldBe(12);
        paragraphs[1].EffectiveProperties.LineSpacing.ShouldBe(2);
    }

    [Fact]
    public void Clear_paragraph_formatting_keeps_list_membership()
    {
        var listId = Guid.NewGuid();
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.Create("Item")),
            new ParagraphProperties
            {
                Alignment = ParagraphAlignment.Right,
                SpaceBeforePoints = 10,
                ListKind = ListKind.Bulleted,
                ListLevel = 3,
                ListId = listId,
            });
        var document = CreateDocument(paragraph);
        var selection = Selection.Caret(TextNavigation.StartOf(paragraph));

        var after = new ApplyParagraphPropertiesCommand(
            selection,
            properties => properties.ListKind == ListKind.None
                ? null
                : new ParagraphProperties
                {
                    ListKind = properties.ListKind,
                    ListLevel = properties.ListLevel,
                    ListId = properties.ListId,
                    StyleId = properties.StyleId,
                }).Apply(document);
        var updated = Paragraphs(after)[0];

        updated.EffectiveProperties.Alignment.ShouldBe(ParagraphAlignment.Left);
        updated.EffectiveProperties.SpaceBeforePoints.ShouldBe(0);
        updated.EffectiveProperties.ListKind.ShouldBe(ListKind.Bulleted);
        updated.EffectiveProperties.ListLevel.ShouldBe(3);
        updated.EffectiveProperties.ListId.ShouldBe(listId);
    }

    [Fact]
    public void Toggling_a_list_assigns_one_list_identity_and_toggles_off()
    {
        var first = ParagraphBlock.Create("First");
        var second = ParagraphBlock.Create("Second");
        var document = CreateDocument(first, second);
        var selection = new Selection(
            TextNavigation.StartOf(first),
            TextNavigation.EndOf(second));
        var command = new ToggleListCommand(selection, ListKind.Numbered);

        var listed = command.Apply(document);
        var paragraphs = Paragraphs(listed);

        paragraphs.ShouldAllBe(paragraph =>
            paragraph.EffectiveProperties.ListKind == ListKind.Numbered);
        paragraphs[0].EffectiveProperties.ListId.ShouldNotBeNull();
        paragraphs[0].EffectiveProperties.ListId.ShouldBe(
            paragraphs[1].EffectiveProperties.ListId);

        var unlisted = new ToggleListCommand(selection, ListKind.Numbered)
            .Apply(listed);

        Paragraphs(unlisted).ShouldAllBe(paragraph =>
            paragraph.EffectiveProperties.ListKind == ListKind.None);
    }

    [Fact]
    public void List_level_accepts_one_through_nine()
    {
        var paragraph = ParagraphBlock.Create("Item");
        var document = CreateDocument(paragraph);
        var selection = Selection.Caret(TextNavigation.StartOf(paragraph));
        var listed = new ToggleListCommand(selection, ListKind.Bulleted)
            .Apply(document);

        var nested = new SetListLevelCommand(selection, 9).Apply(listed);

        Paragraphs(nested)[0].EffectiveProperties.ListLevel.ShouldBe(9);

        Should.Throw<InvalidOperationException>(() =>
            new SetListLevelCommand(selection, 10).Apply(listed));
    }

    [Fact]
    public void Restarting_numbering_assigns_a_new_list_identity_to_contiguous_items()
    {
        var first = ParagraphBlock.Create("One");
        var second = ParagraphBlock.Create("Two");
        var third = ParagraphBlock.Create("Three");
        var document = CreateDocument(first, second, third);
        var selection = new Selection(
            TextNavigation.StartOf(first),
            TextNavigation.EndOf(third));
        var listed = new ToggleListCommand(selection, ListKind.Numbered)
            .Apply(document);
        var originalId = Paragraphs(listed)[0].EffectiveProperties.ListId;

        var restartSelection = Selection.Caret(
            TextNavigation.StartOf(Paragraphs(listed)[1]));
        var restarted = new RestartNumberingCommand(restartSelection).Apply(listed);
        var paragraphs = Paragraphs(restarted);

        paragraphs[0].EffectiveProperties.ListId.ShouldBe(originalId);
        paragraphs[1].EffectiveProperties.ListId.ShouldNotBe(originalId);
        paragraphs[2].EffectiveProperties.ListId.ShouldBe(
            paragraphs[1].EffectiveProperties.ListId);
    }

    [Fact]
    public void Insert_page_break_adds_a_selectable_character_and_undoes()
    {
        var paragraph = ParagraphBlock.Create("BeforeAfter");
        var document = CreateDocument(paragraph);
        var run = paragraph.Runs[0];
        var selection = Selection.Caret(
            new TextPosition(paragraph.Id, run.Id, 6));
        var command = new InsertPageBreakCommand(selection);

        var after = command.Apply(document);

        Paragraphs(after)[0].PlainText.ShouldBe("Before\fAfter");
        command.SelectionAfter.Focus.Offset.ShouldBe(7);
        command.Revert(after).ShouldBeSameAs(document);
    }

    private static Document CreateDocument(params ParagraphBlock[] paragraphs)
    {
        var section = new Section(
            Guid.NewGuid(),
            paragraphs.Cast<Block>().ToImmutableArray());

        return new Document(
            Guid.NewGuid(),
            "Test",
            ImmutableArray.Create(section));
    }

    private static ParagraphBlock[] Paragraphs(Document document) =>
        document.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .ToArray();
}
