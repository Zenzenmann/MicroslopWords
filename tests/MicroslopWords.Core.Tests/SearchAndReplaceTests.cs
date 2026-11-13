using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using Shouldly;

namespace MicroslopWords.Core.Tests;

public sealed class SearchAndReplaceTests
{
    [Fact]
    public void Find_finds_match_spanning_run_boundaries()
    {
        var first = Run.Create("Hel", new RunProperties { Bold = true });
        var second = Run.Create("lo there", new RunProperties { Italic = true });
        var paragraph = ParagraphWithRuns(first, second);
        var document = CreateDocument(paragraph);

        var matches = SearchEngine.Find(document, "lo th");

        matches.Length.ShouldBe(1);
        matches[0].ParagraphId.ShouldBe(paragraph.Id);
        matches[0].StartOffset.ShouldBe(3);
        matches[0].Length.ShouldBe(5);
        matches[0].Text.ShouldBe("lo th");

        var selection = SearchEngine.GetSelection(document, matches[0]);

        TextNavigation.GetParagraphTextOffset(
                paragraph,
                selection.Anchor.RunId,
                selection.Anchor.Offset)
            .ShouldBe(3);
        TextNavigation.GetParagraphTextOffset(
                paragraph,
                selection.Focus.RunId,
                selection.Focus.Offset)
            .ShouldBe(8);
    }

    [Fact]
    public void Find_honors_case_and_whole_word_options()
    {
        var document = CreateDocument(ParagraphBlock.Create("cat dog Cat scatter"));

        SearchEngine.Find(document, "cat").Length.ShouldBe(3);
        SearchEngine.Find(document, "cat", new FindOptions(MatchCase: true))
            .Length.ShouldBe(2);
        SearchEngine.Find(document, "cat", new FindOptions(WholeWords: true))
            .Length.ShouldBe(2);
        SearchEngine.Find(
                document,
                "cat",
                new FindOptions(MatchCase: true, WholeWords: true))
            .Length.ShouldBe(1);
    }

    [Fact]
    public void Find_with_empty_query_returns_no_matches_without_mutating_document()
    {
        var document = CreateDocument(ParagraphBlock.Create("Hello world"));

        SearchEngine.Find(document, string.Empty).ShouldBeEmpty();

        document.PlainText.ShouldBe("Hello world");
    }

    [Fact]
    public void Find_does_not_match_across_paragraphs()
    {
        var document = CreateDocument(
            ParagraphBlock.Create("Hello"),
            ParagraphBlock.Create("world"));

        SearchEngine.Find(document, "lo wo").ShouldBeEmpty();
    }

    [Fact]
    public void Find_does_not_mutate_the_document()
    {
        var document = CreateDocument(ParagraphBlock.Create("one one one"));
        var before = document.PlainText;

        var matches = SearchEngine.Find(document, "one");

        matches.Length.ShouldBe(3);
        document.PlainText.ShouldBe(before);
    }

    [Fact]
    public void ReplaceMatch_replaces_across_runs_and_inherits_start_formatting()
    {
        var first = Run.Create("Hel", new RunProperties { Bold = true });
        var second = Run.Create("lo there", new RunProperties { Italic = true });
        var paragraph = ParagraphWithRuns(first, second);
        var document = CreateDocument(paragraph);
        var match = SearchEngine.Find(document, "lo th").Single();
        var history = new CommandHistory(document);

        history.Execute(new ReplaceMatchCommand(history.Selection, match, "X"));

        var updated = Paragraphs(history.Current).Single();
        updated.PlainText.ShouldBe("HelXere");
        updated.Runs.Single(run => run.Text == "Hel").Id.ShouldBe(first.Id);
        updated.Runs.Single(run => run.Text == "Hel").Properties!.Bold.ShouldBeTrue();
        updated.Runs.Single(run => run.Text == "X").Properties!.Italic.ShouldBeTrue();
        updated.Runs.Count(run => run.Text == "ere").ShouldBe(1);
        updated.Runs.Single(run => run.Text == "ere").Id.ShouldBe(second.Id);

        var caret = history.Selection.Focus;
        TextNavigation.GetParagraphTextOffset(
                updated,
                caret.RunId,
                caret.Offset)
            .ShouldBe(4);

        history.Undo();
        history.Current.ShouldBeSameAs(document);
        history.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void ReplaceMatch_with_empty_replacement_deletes_only_the_match()
    {
        var document = CreateDocument(ParagraphBlock.Create("foo bar foo"));
        var match = SearchEngine.Find(document, "foo").First();
        var history = new CommandHistory(document);

        history.Execute(new ReplaceMatchCommand(history.Selection, match, string.Empty));

        Paragraphs(history.Current).Single().PlainText.ShouldBe(" bar foo");
        Paragraphs(history.Current).Length.ShouldBe(1);
    }

    [Fact]
    public void ReplaceAll_replaces_every_match_in_one_undo_step()
    {
        var document = CreateDocument(ParagraphBlock.Create("one one one"));
        var history = new CommandHistory(document);

        SearchEngine.Find(document, "one").Length.ShouldBe(3);
        history.Execute(new ReplaceAllCommand(history.Selection, "one", "two"));

        Paragraphs(history.Current).Single().PlainText.ShouldBe("two two two");
        history.CanUndo.ShouldBeTrue();

        history.Undo();

        history.Current.ShouldBeSameAs(document);
        history.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void ReplaceAll_terminates_when_replacement_contains_query()
    {
        var document = CreateDocument(ParagraphBlock.Create("a a"));
        var history = new CommandHistory(document);

        history.Execute(new ReplaceAllCommand(history.Selection, "a", "aa"));

        Paragraphs(history.Current).Single().PlainText.ShouldBe("aa aa");

        history.Undo();

        history.Current.PlainText.ShouldBe("a a");
    }

    [Fact]
    public void ReplaceAll_preserves_untouched_runs_and_their_ids()
    {
        var prefix = Run.Create("Hello ", new RunProperties { Bold = true });
        var target = Run.Create("world", new RunProperties { Italic = true });
        var suffix = Run.Create("!", new RunProperties { Underline = true });
        var paragraph = ParagraphWithRuns(prefix, target, suffix);
        var document = CreateDocument(paragraph);
        var history = new CommandHistory(document);

        history.Execute(new ReplaceAllCommand(
            history.Selection,
            "world",
            "there",
            new FindOptions(MatchCase: true, WholeWords: true)));

        var updated = Paragraphs(history.Current).Single();
        updated.PlainText.ShouldBe("Hello there!");
        updated.Runs.Single(run => run.Text == "Hello ").Id.ShouldBe(prefix.Id);
        updated.Runs.Single(run => run.Text == "!").Id.ShouldBe(suffix.Id);
        updated.Runs.Single(run => run.Text == "there").Properties!.Italic.ShouldBeTrue();
        updated.Runs.Any(run => run.Id == target.Id).ShouldBeFalse();
    }

    [Fact]
    public void ReplaceAll_honors_whole_word_option_and_case_option()
    {
        var document = CreateDocument(ParagraphBlock.Create("cat scatter Cat"));
        var history = new CommandHistory(document);

        history.Execute(new ReplaceAllCommand(
            history.Selection,
            "cat",
            "dog",
            new FindOptions(MatchCase: true, WholeWords: true)));

        Paragraphs(history.Current).Single().PlainText.ShouldBe("dog scatter Cat");
    }

    [Fact]
    public void ReplaceAll_reports_no_change_when_no_matches_exist()
    {
        var document = CreateDocument(ParagraphBlock.Create("Hello"));
        var history = new CommandHistory(document);
        var beforeSelection = history.Selection;

        history.Execute(new ReplaceAllCommand(history.Selection, "missing", "x"));

        history.Current.ShouldBeSameAs(document);
        history.Selection.ShouldBe(beforeSelection);
    }

    [Fact]
    public void ReplaceAll_replaces_matches_in_multiple_paragraphs()
    {
        var document = CreateDocument(
            ParagraphBlock.Create("foo one"),
            ParagraphBlock.Create("two foo"));
        var history = new CommandHistory(document);

        history.Execute(new ReplaceAllCommand(history.Selection, "foo", "bar"));

        var paragraphs = Paragraphs(history.Current);
        paragraphs[0].PlainText.ShouldBe("bar one");
        paragraphs[1].PlainText.ShouldBe("two bar");
    }

    private static ParagraphBlock ParagraphWithRuns(params Run[] runs) =>
        new(Guid.NewGuid(), runs.ToImmutableArray());

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
