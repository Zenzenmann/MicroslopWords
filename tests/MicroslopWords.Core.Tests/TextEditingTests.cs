using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using Shouldly;

namespace MicroslopWords.Core.Tests;

public sealed class TextEditingTests
{
    [Fact]
    public void InsertTextAtSelection_inserts_at_caret_and_advances_selection()
    {
        var document = CreateDocument("ab");
        var paragraph = Paragraphs(document)[0];
        var run = paragraph.Runs[0];
        var history = new CommandHistory(
            document,
            Selection.Caret(new TextPosition(paragraph.Id, run.Id, 1)));

        history.Execute(new InsertTextAtSelectionCommand(
            history.Selection,
            "X"));

        Paragraphs(history.Current)[0].PlainText.ShouldBe("aXb");
        history.Selection.Focus.Offset.ShouldBe(2);
        history.Undo();
        history.Current.PlainText.ShouldBe("ab");
        history.Redo();
        history.Current.PlainText.ShouldBe("aXb");
    }

    [Fact]
    public void InsertTextAtSelection_replaces_selection_in_one_undo_step()
    {
        var document = CreateDocument("Hello world");
        var paragraph = Paragraphs(document)[0];
        var run = paragraph.Runs[0];
        var before = document;
        var history = new CommandHistory(
            document,
            Selection.Caret(new TextPosition(paragraph.Id, run.Id, 0)));

        history.Execute(new InsertTextAtSelectionCommand(
            new Selection(
                new TextPosition(paragraph.Id, run.Id, 6),
                new TextPosition(paragraph.Id, run.Id, 11)),
            "there"));

        Paragraphs(history.Current)[0].PlainText.ShouldBe("Hello there");

        history.Undo();
        history.Current.ShouldBeSameAs(before);
        history.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void InsertTextAtSelection_preserves_supplementary_plane_characters()
    {
        var document = CreateDocument("ab");
        var paragraph = Paragraphs(document)[0];
        var run = paragraph.Runs[0];
        var history = new CommandHistory(
            document,
            Selection.Caret(new TextPosition(paragraph.Id, run.Id, 1)));

        history.Execute(new InsertTextAtSelectionCommand(
            history.Selection,
            "\U0001F600"));

        Paragraphs(history.Current)[0].Runs[0].Text.ShouldBe("a\U0001F600b");
        history.Selection.Focus.Offset.ShouldBe(3);
    }

    [Fact]
    public void DeleteBackward_at_paragraph_start_merges_with_previous()
    {
        var document = CreateDocument("Hello", "World");
        var second = Paragraphs(document)[1];
        var history = new CommandHistory(
            document,
            Selection.Caret(new TextPosition(second.Id, second.Runs[0].Id, 0)));

        history.Execute(new DeleteBackwardCommand(history.Selection));

        var paragraphs = Paragraphs(history.Current);
        paragraphs.Length.ShouldBe(1);
        paragraphs[0].PlainText.ShouldBe("HelloWorld");
        history.Selection.Focus.Offset.ShouldBe(5);
    }

    [Fact]
    public void DeleteForward_at_paragraph_end_merges_with_next()
    {
        var document = CreateDocument("Hello", "World");
        var first = Paragraphs(document)[0];
        var history = new CommandHistory(
            document,
            Selection.Caret(new TextPosition(
                first.Id,
                first.Runs[0].Id,
                first.Runs[0].Text.Length)));

        history.Execute(new DeleteForwardCommand(history.Selection));

        var paragraphs = Paragraphs(history.Current);
        paragraphs.Length.ShouldBe(1);
        paragraphs[0].PlainText.ShouldBe("HelloWorld");
        history.Selection.Focus.Offset.ShouldBe(5);
    }

    [Fact]
    public void SplitParagraph_preserves_text_order_formatting_and_paragraph_properties()
    {
        var bold = new RunProperties { Bold = true };
        var firstRun = Run.Create("Hello ", new RunProperties { Italic = true });
        var secondRun = Run.Create("world", bold);
        var properties = new ParagraphProperties { SpaceBeforePoints = 4 };
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(firstRun, secondRun),
            properties);
        var document = CreateDocument(paragraph);
        var history = new CommandHistory(
            document,
            Selection.Caret(new TextPosition(paragraph.Id, secondRun.Id, 2)));

        history.Execute(new SplitParagraphCommand(history.Selection));

        var paragraphs = Paragraphs(history.Current);
        paragraphs.Length.ShouldBe(2);
        paragraphs[0].PlainText.ShouldBe("Hello wo");
        paragraphs[1].PlainText.ShouldBe("rld");
        paragraphs[1].EffectiveProperties.ShouldBe(properties);
        paragraphs[1].Runs[0].EffectiveProperties.ShouldBe(bold);
        history.Selection.Focus.ParagraphId.ShouldBe(paragraphs[1].Id);
        history.Selection.Focus.Offset.ShouldBe(0);
    }

    [Fact]
    public void SplitParagraph_at_paragraph_start_creates_an_empty_first_paragraph()
    {
        var document = CreateDocument("Hello");
        var paragraph = Paragraphs(document)[0];
        var history = new CommandHistory(
            document,
            Selection.Caret(new TextPosition(paragraph.Id, paragraph.Runs[0].Id, 0)));

        history.Execute(new SplitParagraphCommand(history.Selection));

        var paragraphs = Paragraphs(history.Current);
        paragraphs[0].PlainText.ShouldBeEmpty();
        paragraphs[1].PlainText.ShouldBe("Hello");
    }

    [Fact]
    public void InsertSoftBreak_keeps_one_paragraph_and_text_order()
    {
        var document = CreateDocument("Hello world");
        var paragraph = Paragraphs(document)[0];
        var history = new CommandHistory(
            document,
            Selection.Caret(new TextPosition(paragraph.Id, paragraph.Runs[0].Id, 5)));

        history.Execute(new InsertSoftBreakCommand(history.Selection));

        var paragraphs = Paragraphs(history.Current);
        paragraphs.Length.ShouldBe(1);
        paragraphs[0].PlainText.ShouldBe("Hello\n world");
        history.Selection.Focus.Offset.ShouldBe(6);
    }

    [Fact]
    public void DeleteSelection_across_runs_preserves_remaining_runs()
    {
        var firstRun = Run.Create("Hello ");
        var secondRun = Run.Create("world");
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(firstRun, secondRun));
        var document = CreateDocument(paragraph);
        var selection = new Selection(
            new TextPosition(paragraph.Id, firstRun.Id, 6),
            new TextPosition(paragraph.Id, secondRun.Id, 5));

        var command = new DeleteSelectionCommand(selection);
        var after = command.Apply(document);

        Paragraphs(after)[0].PlainText.ShouldBe("Hello ");
        Paragraphs(after)[0].Runs[0].Id.ShouldBe(firstRun.Id);
    }

    [Fact]
    public void DeleteSelection_across_paragraphs_merges_them_and_keeps_one_paragraph()
    {
        var document = CreateDocument("Hello", "World");
        var first = Paragraphs(document)[0];
        var second = Paragraphs(document)[1];
        var history = new CommandHistory(
            document,
            Selection.Caret(new TextPosition(first.Id, first.Runs[0].Id, 0)));

        history.Execute(new DeleteSelectionCommand(new Selection(
            new TextPosition(first.Id, first.Runs[0].Id, 5),
            new TextPosition(second.Id, second.Runs[0].Id, 5))));

        var paragraphs = Paragraphs(history.Current);
        paragraphs.Length.ShouldBe(1);
        paragraphs[0].PlainText.ShouldBe("Hello");
    }

    [Fact]
    public void DeleteWordBackward_deletes_previous_word()
    {
        var document = CreateDocument("Hello brave world");
        var paragraph = Paragraphs(document)[0];
        var run = paragraph.Runs[0];
        var history = new CommandHistory(
            document,
            Selection.Caret(new TextPosition(paragraph.Id, run.Id, run.Text.Length)));

        history.Execute(new DeleteWordBackwardCommand(history.Selection));

        Paragraphs(history.Current)[0].PlainText.ShouldBe("Hello brave ");
        history.Selection.Focus.Offset.ShouldBe(12);
    }

    [Fact]
    public void DeleteWordForward_deletes_next_word_and_following_space()
    {
        var document = CreateDocument("Hello brave world");
        var paragraph = Paragraphs(document)[0];
        var history = new CommandHistory(
            document,
            Selection.Caret(new TextPosition(paragraph.Id, paragraph.Runs[0].Id, 0)));

        history.Execute(new DeleteWordForwardCommand(history.Selection));

        Paragraphs(history.Current)[0].PlainText.ShouldBe("brave world");
    }

    [Fact]
    public void Deleting_the_only_content_keeps_one_empty_paragraph()
    {
        var document = CreateDocument("Everything");
        var paragraph = Paragraphs(document)[0];
        var run = paragraph.Runs[0];
        var history = new CommandHistory(
            document,
            Selection.Caret(new TextPosition(paragraph.Id, run.Id, 0)));

        history.Execute(new DeleteSelectionCommand(new Selection(
            new TextPosition(paragraph.Id, run.Id, 0),
            new TextPosition(paragraph.Id, run.Id, run.Text.Length))));

        var paragraphs = Paragraphs(history.Current);
        paragraphs.Length.ShouldBe(1);
        paragraphs[0].PlainText.ShouldBeEmpty();
    }

    [Fact]
    public void CommandHistory_normalizes_selection_after_an_unmanaged_command()
    {
        var document = Document.CreateEmpty();
        var paragraph = (ParagraphBlock)document.Sections[0].Blocks[0];
        var history = new CommandHistory(document);

        history.SetSelection(Selection.Caret(new TextPosition(
            paragraph.Id,
            Guid.NewGuid(),
            99)));

        history.Selection.Focus.ParagraphId.ShouldBe(paragraph.Id);
        history.Selection.Focus.Offset.ShouldBe(0);
    }

    private static Document CreateDocument(params string[] paragraphs) =>
        CreateDocument(paragraphs
            .Select(ParagraphBlock.Create)
            .ToArray());

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
