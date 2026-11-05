using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using Shouldly;

namespace MicroslopWords.Core.Tests;

public sealed class ClipboardHistoryTests
{
    [Fact]
    public void Contiguous_typing_coalesces_until_the_typing_window_closes()
    {
        var clock = new TestTimeProvider();
        var (document, paragraph, run) = CreateDocument("Hello");
        var history = CreateHistory(document, paragraph, run, clock, offset: 5);

        history.Execute(new InsertTextAtSelectionCommand(history.Selection, "a"));
        clock.Advance(TimeSpan.FromMilliseconds(500));
        history.Execute(new InsertTextAtSelectionCommand(history.Selection, "b"));

        history.Current.PlainText.ShouldBe("Helloab");
        history.Undo();
        history.Current.PlainText.ShouldBe("Hello");
        history.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void Typing_after_a_one_second_pause_starts_a_new_undo_step()
    {
        var clock = new TestTimeProvider();
        var (document, paragraph, run) = CreateDocument("Hello");
        var history = CreateHistory(document, paragraph, run, clock, offset: 5);

        history.Execute(new InsertTextAtSelectionCommand(history.Selection, "a"));
        clock.Advance(TimeSpan.FromMilliseconds(1100));
        history.Execute(new InsertTextAtSelectionCommand(history.Selection, "b"));

        history.Undo();
        history.Current.PlainText.ShouldBe("Helloa");
        history.Undo();
        history.Current.PlainText.ShouldBe("Hello");
    }

    [Fact]
    public void Moving_the_caret_breaks_typing_coalescing()
    {
        var clock = new TestTimeProvider();
        var (document, paragraph, run) = CreateDocument("Hello");
        var history = CreateHistory(document, paragraph, run, clock, offset: 5);

        history.Execute(new InsertTextAtSelectionCommand(history.Selection, "a"));
        history.SetSelection(Selection.Caret(new TextPosition(paragraph.Id, run.Id, 0)));
        history.Execute(new InsertTextAtSelectionCommand(history.Selection, "b"));

        history.Current.PlainText.ShouldBe("bHelloa");
        history.Undo();
        history.Current.PlainText.ShouldBe("Helloa");
        history.Undo();
        history.Current.PlainText.ShouldBe("Hello");
    }

    [Fact]
    public void A_new_edit_clears_redo_after_undo()
    {
        var clock = new TestTimeProvider();
        var (document, paragraph, run) = CreateDocument("Hello");
        var history = CreateHistory(document, paragraph, run, clock, offset: 5);

        history.Execute(new InsertTextAtSelectionCommand(history.Selection, "a"));
        history.Undo();
        history.CanRedo.ShouldBeTrue();

        history.Execute(new InsertTextAtSelectionCommand(history.Selection, "b"));

        history.CanRedo.ShouldBeFalse();
        history.Current.PlainText.ShouldBe("Hellob");
    }

    [Fact]
    public void Soft_breaks_do_not_coalesce_with_adjacent_typing()
    {
        var clock = new TestTimeProvider();
        var (document, paragraph, run) = CreateDocument("Hello");
        var history = CreateHistory(document, paragraph, run, clock, offset: 5);

        history.Execute(new InsertTextAtSelectionCommand(history.Selection, "a"));
        clock.Advance(TimeSpan.FromMilliseconds(100));
        history.Execute(new InsertSoftBreakCommand(history.Selection));
        clock.Advance(TimeSpan.FromMilliseconds(100));
        history.Execute(new InsertTextAtSelectionCommand(history.Selection, "b"));

        history.Current.PlainText.ShouldBe("Helloa\nb");
        history.Undo();
        history.Current.PlainText.ShouldBe("Helloa\n");
        history.Undo();
        history.Current.PlainText.ShouldBe("Helloa");
        history.Undo();
        history.Current.PlainText.ShouldBe("Hello");
    }

    [Fact]
    public void Selection_survives_an_unrelated_edit_and_is_normalized()
    {
        var document = Document.CreateEmpty();
        var paragraph = (ParagraphBlock)document.Sections[0].Blocks[0];
        var history = new CommandHistory(
            document,
            Selection.Caret(new TextPosition(paragraph.Id, Guid.Empty, 0)));

        history.Execute(new InsertTextCommand(paragraph.Id, Run.Create("Hello")));

        history.SetSelection(Selection.Caret(new TextPosition(
            paragraph.Id,
            history.Current.FindParagraph(paragraph.Id)!.Runs[0].Id,
            2)));
        history.Execute(new InsertTextCommand(paragraph.Id, Run.Create("!")));

        history.Selection.Focus.Offset.ShouldBe(2);
    }

    [Fact]
    public void Capturing_and_pasting_a_fragment_preserves_runs_and_paragraphs()
    {
        var firstRun = Run.Create("Hello ", new RunProperties { Italic = true });
        var secondRun = Run.Create("world", new RunProperties { Bold = true });
        var paragraphProperties = new ParagraphProperties { SpaceBeforePoints = 4 };
        var sourceParagraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(firstRun, secondRun),
            paragraphProperties);
        var source = CreateDocument(sourceParagraph);
        var selection = new Selection(
            new TextPosition(sourceParagraph.Id, secondRun.Id, 0),
            new TextPosition(sourceParagraph.Id, secondRun.Id, 5));

        var fragment = DocumentFragment.Capture(source, selection);

        fragment.Paragraphs.Length.ShouldBe(1);
        fragment.Paragraphs[0].Runs.Single().Text.ShouldBe("world");
        fragment.Paragraphs[0].Runs.Single().Properties!.Bold.ShouldBeTrue();

        var targetDocument = Document.CreateEmpty();
        var targetParagraph = (ParagraphBlock)targetDocument.Sections[0].Blocks[0];
        var command = new PasteFragmentCommand(
            Selection.StartOf(targetDocument),
            fragment);
        var after = command.Apply(targetDocument);
        var pasted = (ParagraphBlock)after.Sections[0].Blocks[0];

        pasted.PlainText.ShouldBe("world");
        pasted.Runs.Single().EffectiveProperties.Bold.ShouldBeTrue();
    }

    [Fact]
    public void Fragment_paste_round_trips_paragraph_properties_and_soft_breaks()
    {
        var properties = new ParagraphProperties
        {
            SpaceBeforePoints = 12,
            LineSpacing = 2,
        };
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.Create("a\nb")),
            properties);
        var source = CreateDocument(paragraph);
        var fragment = DocumentFragment.Capture(
            source,
            new Selection(
                TextNavigation.StartOf(paragraph),
                TextNavigation.EndOf(paragraph)));

        var target = Document.CreateEmpty();
        var command = new PasteFragmentCommand(
            Selection.StartOf(target),
            fragment);
        var after = command.Apply(target);
        var pasted = (ParagraphBlock)after.Sections[0].Blocks[0];

        pasted.PlainText.ShouldBe("a\nb");
        pasted.EffectiveProperties.ShouldBe(properties);
    }

    [Fact]
    public void Multi_paragraph_fragment_pastes_as_separate_paragraphs()
    {
        var first = ParagraphBlock.Create("First");
        var second = ParagraphBlock.Create("Second");
        var source = CreateDocument(first, second);
        var selection = new Selection(
            TextNavigation.StartOf(first),
            TextNavigation.EndOf(second));
        var fragment = DocumentFragment.Capture(source, selection);

        fragment.Paragraphs.Length.ShouldBe(2);

        var targetDocument = Document.CreateEmpty();
        var command = new PasteFragmentCommand(
            Selection.StartOf(targetDocument),
            fragment);
        var after = command.Apply(targetDocument);
        var paragraphs = after.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .ToArray();

        paragraphs.Length.ShouldBe(2);
        paragraphs[0].PlainText.ShouldBe("First");
        paragraphs[1].PlainText.ShouldBe("Second");
    }

    [Fact]
    public void Plain_text_paste_maps_line_breaks_to_paragraphs()
    {
        var document = Document.CreateEmpty();
        var command = new PasteTextCommand(
            Selection.StartOf(document),
            "first\r\nsecond\nthird");

        var after = command.Apply(document);
        var paragraphs = after.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .ToArray();

        paragraphs.Length.ShouldBe(3);
        paragraphs.Select(paragraph => paragraph.PlainText)
            .ShouldBe(["first", "second", "third"]);
    }

    [Fact]
    public void Word_and_paragraph_selection_helpers_cover_the_right_ranges()
    {
        var (document, paragraph, run) = CreateDocument("Hello brave world");
        var inside = new TextPosition(paragraph.Id, run.Id, 8);

        var word = TextNavigation.GetWordSelection(document, inside);
        var paragraphSelection = TextNavigation.GetParagraphSelection(document, inside);

        word.GetStart(document).Offset.ShouldBe(6);
        word.GetEnd(document).Offset.ShouldBe(11);
        paragraphSelection.GetStart(document).Offset.ShouldBe(0);
        paragraphSelection.GetEnd(document).Offset.ShouldBe(17);
    }

    [Fact]
    public void Word_navigation_moves_between_words_and_paragraphs()
    {
        var first = ParagraphBlock.Create("Hello brave");
        var second = ParagraphBlock.Create("world");
        var document = CreateDocument(first, second);
        var firstRun = first.Runs[0];

        var next = TextNavigation.NextWord(
            document,
            new TextPosition(first.Id, firstRun.Id, 0));
        var previous = TextNavigation.PreviousWord(
            document,
            new TextPosition(first.Id, firstRun.Id, 5));

        next.Offset.ShouldBe(6);
        previous.Offset.ShouldBe(0);

        var nextParagraph = TextNavigation.NextWord(
            document,
            new TextPosition(first.Id, firstRun.Id, firstRun.Text.Length));
        nextParagraph.ParagraphId.ShouldBe(second.Id);
        nextParagraph.Offset.ShouldBe(0);
    }

    private static CommandHistory CreateHistory(
        Document document,
        ParagraphBlock paragraph,
        Run run,
        TimeProvider clock,
        int offset) =>
        new(
            document,
            Selection.Caret(new TextPosition(paragraph.Id, run.Id, offset)),
            clock);

    private static (Document Document, ParagraphBlock Paragraph, Run Run) CreateDocument(
        string text)
    {
        var run = Run.Create(text);
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(run));
        var document = CreateDocument(paragraph);

        return (document, paragraph, run);
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

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }
}
