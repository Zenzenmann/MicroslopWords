using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using Shouldly;

namespace MicroslopWords.Core.Tests;

public sealed class SelectionTests
{
    [Fact]
    public void Selection_caret_clamps_to_run_and_keeps_grapheme_boundary()
    {
        var (document, paragraph, run) = CreateDocument("Hello");

        var selection = SelectionMap.Normalize(
            Selection.Caret(new TextPosition(paragraph.Id, run.Id, 99)),
            document);

        selection.IsCollapsed.ShouldBeTrue();
        selection.Focus.Offset.ShouldBe(5);
    }

    [Fact]
    public void Selection_normalization_snaps_offsets_outside_a_surrogate_pair()
    {
        var (document, paragraph, run) = CreateDocument("a\U0001F600b");

        var downstream = SelectionMap.Normalize(
            Selection.Caret(new TextPosition(
                paragraph.Id,
                run.Id,
                2,
                TextAffinity.Downstream)),
            document);

        var upstream = SelectionMap.Normalize(
            Selection.Caret(new TextPosition(
                paragraph.Id,
                run.Id,
                2,
                TextAffinity.Upstream)),
            document);

        downstream.Focus.Offset.ShouldBe(1);
        upstream.Focus.Offset.ShouldBe(3);
    }

    [Fact]
    public void Selection_order_uses_document_order_not_anchor_direction()
    {
        var (document, paragraph, run) = CreateDocument("Hello");
        var first = new TextPosition(paragraph.Id, run.Id, 1);
        var second = new TextPosition(paragraph.Id, run.Id, 4);
        var selection = new Selection(second, first);

        selection.GetStart(document).Offset.ShouldBe(1);
        selection.GetEnd(document).Offset.ShouldBe(4);
    }

    [Fact]
    public void Selection_normalization_falls_back_when_paragraph_is_gone()
    {
        var (document, paragraph, run) = CreateDocument("Hello");

        var selection = SelectionMap.Normalize(
            Selection.Caret(new TextPosition(Guid.NewGuid(), run.Id, 2)),
            document);

        selection.Focus.ParagraphId.ShouldBe(paragraph.Id);
        selection.Focus.Offset.ShouldBe(0);
    }

    private static (Document Document, ParagraphBlock Paragraph, Run Run) CreateDocument(
        string text)
    {
        var run = Run.Create(text);
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(run));
        var section = new Section(
            Guid.NewGuid(),
            ImmutableArray.Create<Block>(paragraph));
        var document = new Document(
            Guid.NewGuid(),
            "Test",
            ImmutableArray.Create(section));

        return (document, paragraph, run);
    }
}
