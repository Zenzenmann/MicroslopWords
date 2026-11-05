using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using Shouldly;

namespace MicroslopWords.Core.Tests;

public sealed class CharacterFormattingTests
{
    [Fact]
    public void Format_selection_changes_only_the_selected_text_and_undoes_exactly()
    {
        var run = Run.Create("Hello world");
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(run));
        var document = CreateDocument(paragraph);
        var before = document;
        var selection = new Selection(
            new TextPosition(paragraph.Id, run.Id, 6),
            new TextPosition(paragraph.Id, run.Id, 11));

        var command = new FormatSelectionCommand(
            selection,
            properties => properties with { Bold = true });
        var after = command.Apply(document);
        var updated = (ParagraphBlock)after.Sections[0].Blocks[0];

        updated.PlainText.ShouldBe("Hello world");
        updated.Runs.Single(candidate => candidate.Text == "Hello ")
            .Properties.ShouldBeNull();
        updated.Runs.Single(candidate => candidate.Text == "world")
            .EffectiveProperties.Bold.ShouldBeTrue();
        var selectionStart = command.SelectionAfter.GetStart(after);
        var selectionEnd = command.SelectionAfter.GetEnd(after);
        TextNavigation.GetParagraphTextOffset(
                updated,
                selectionStart.RunId,
                selectionStart.Offset)
            .ShouldBe(6);
        TextNavigation.GetParagraphTextOffset(
                updated,
                selectionEnd.RunId,
                selectionEnd.Offset)
            .ShouldBe(11);

        command.Revert(after).ShouldBeSameAs(before);
    }

    [Fact]
    public void Format_selection_spans_runs_without_touching_outside_text()
    {
        var first = Run.Create("abc", new RunProperties { Italic = true });
        var second = Run.Create("def");
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(first, second));
        var document = CreateDocument(paragraph);
        var selection = new Selection(
            new TextPosition(paragraph.Id, first.Id, 1),
            new TextPosition(paragraph.Id, second.Id, 2));

        var after = new FormatSelectionCommand(
            selection,
            properties => properties with { Underline = true })
            .Apply(document);
        var updated = (ParagraphBlock)after.Sections[0].Blocks[0];

        updated.PlainText.ShouldBe("abcdef");
        updated.Runs.Single(run => run.Text == "a")
            .EffectiveProperties.Underline.ShouldBeFalse();
        updated.Runs.Single(run => run.Text == "bc")
            .EffectiveProperties.Underline.ShouldBeTrue();
        updated.Runs.Single(run => run.Text == "de")
            .EffectiveProperties.Underline.ShouldBeTrue();
        updated.Runs.Single(run => run.Text == "f")
            .EffectiveProperties.Underline.ShouldBeFalse();
    }

    [Fact]
    public void Clear_formatting_removes_direct_overrides()
    {
        var run = Run.Create("Bold", new RunProperties { Bold = true });
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(run));
        var document = CreateDocument(paragraph);
        var selection = new Selection(
            new TextPosition(paragraph.Id, run.Id, 0),
            new TextPosition(paragraph.Id, run.Id, 4));

        var after = new FormatSelectionCommand(selection, _ => null).Apply(document);
        var updated = (ParagraphBlock)after.Sections[0].Blocks[0];

        updated.Runs[0].Properties.ShouldBeNull();
        updated.Runs[0].EffectiveProperties.Bold.ShouldBeFalse();
    }

    [Fact]
    public void Insertion_formatting_creates_a_styled_run_without_restyling_adjacent_text()
    {
        var run = Run.Create("ab");
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(run));
        var document = CreateDocument(paragraph);
        var caret = Selection.Caret(new TextPosition(paragraph.Id, run.Id, 1));

        var command = new InsertTextAtSelectionCommand(
            caret,
            "X",
            new RunProperties { Bold = true, ColorHex = "#FF0000" });
        var after = command.Apply(document);
        var updated = (ParagraphBlock)after.Sections[0].Blocks[0];

        updated.PlainText.ShouldBe("aXb");
        updated.Runs.Single(candidate => candidate.Text == "a")
            .EffectiveProperties.Bold.ShouldBeFalse();
        updated.Runs.Single(candidate => candidate.Text == "X")
            .EffectiveProperties.Bold.ShouldBeTrue();
        updated.Runs.Single(candidate => candidate.Text == "b")
            .EffectiveProperties.Bold.ShouldBeFalse();
        command.SelectionAfter.Focus.Offset.ShouldBe(1);
    }

    private static Document CreateDocument(ParagraphBlock paragraph)
    {
        var section = new Section(
            Guid.NewGuid(),
            ImmutableArray.Create<Block>(paragraph));

        return new Document(
            Guid.NewGuid(),
            "Test",
            ImmutableArray.Create(section));
    }
}
