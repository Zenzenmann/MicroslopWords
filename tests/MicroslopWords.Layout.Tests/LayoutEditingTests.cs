using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using Shouldly;

namespace MicroslopWords.Layout.Tests;

public sealed class LayoutEditingTests
{
    [Fact]
    public void Layout_soft_break_forces_a_new_line_without_a_new_paragraph()
    {
        var document = CreateDocument("Hello\nworld");

        var layout = new LayoutEngine().Layout(document);
        var paragraph = layout.Pages[0].Paragraphs.Single();
        var lines = paragraph.Lines;

        lines.Length.ShouldBe(2);
        lines[0].Text.ShouldBe("Hello");
        lines[0].TextStart.ShouldBe(0);
        lines[1].Text.ShouldBe("world");
        lines[1].TextStart.ShouldBe(6);
        lines[1].Bounds.Y.ShouldBe(lines[0].Bounds.Bottom, 0.0001);
    }

    [Fact]
    public void Layout_empty_soft_break_segments_produce_empty_lines()
    {
        var document = CreateDocument("a\n\nb");

        var layout = new LayoutEngine().Layout(document);
        var lines = layout.Pages[0].Paragraphs.Single().Lines;

        lines.Length.ShouldBe(3);
        lines[0].Text.ShouldBe("a");
        lines[0].TextStart.ShouldBe(0);
        lines[1].Text.ShouldBeEmpty();
        lines[1].TextStart.ShouldBe(2);
        lines[2].Text.ShouldBe("b");
        lines[2].TextStart.ShouldBe(3);
    }

    [Fact]
    public void Layout_find_caret_maps_paragraph_offsets_to_line_rectangles()
    {
        var document = CreateDocument("Hello world");
        var layout = new LayoutEngine().Layout(document);
        var paragraph = layout.Pages[0].Paragraphs.Single();
        var firstLine = paragraph.Lines[0];

        var start = LayoutHitTesting.FindCaret(layout, paragraph.ParagraphId, 0);

        start.ShouldNotBeNull();
        start.Bounds.X.ShouldBe(firstLine.Bounds.X, 0.0001);
        start.Bounds.Y.ShouldBe(firstLine.Bounds.Y, 0.0001);

        var five = LayoutHitTesting.FindCaret(layout, paragraph.ParagraphId, 5);

        five.ShouldNotBeNull();
        five.Bounds.X.ShouldBe(firstLine.Bounds.X + (5 * 5.5), 0.0001);
    }

    [Fact]
    public void Layout_find_caret_prefers_upstream_line_at_a_soft_break()
    {
        var document = CreateDocument("a\nb");
        var layout = new LayoutEngine().Layout(document);
        var paragraph = layout.Pages[0].Paragraphs.Single();

        var beforeBreak = LayoutHitTesting.FindCaret(
            layout,
            paragraph.ParagraphId,
            1,
            TextAffinity.Upstream);

        beforeBreak.ShouldNotBeNull();
        beforeBreak.LineIndex.ShouldBe(0);
        beforeBreak.Bounds.X.ShouldBe(paragraph.Lines[0].Bounds.Right, 0.0001);

        var afterBreak = LayoutHitTesting.FindCaret(
            layout,
            paragraph.ParagraphId,
            2,
            TextAffinity.Downstream);

        afterBreak.ShouldNotBeNull();
        afterBreak.LineIndex.ShouldBe(1);
    }

    [Fact]
    public void Layout_find_caret_uses_the_new_line_after_a_trailing_soft_break()
    {
        var document = CreateDocument("a\n");
        var layout = new LayoutEngine().Layout(document);
        var paragraph = layout.Pages[0].Paragraphs.Single();

        var caret = LayoutHitTesting.FindCaret(
            layout,
            paragraph.ParagraphId,
            2,
            TextAffinity.Upstream);

        caret.ShouldNotBeNull();
        caret.LineIndex.ShouldBe(1);
        caret.Line.IsAfterSoftBreak.ShouldBeTrue();
    }

    [Fact]
    public void Layout_hit_test_maps_x_to_nearest_grapheme_boundary()
    {
        var document = CreateDocument("a\U0001F600b");
        var layout = new LayoutEngine().Layout(document);
        var paragraph = layout.Pages[0].Paragraphs.Single();
        var line = paragraph.Lines[0];

        var insideEmoji = LayoutHitTesting.HitTestLine(
            layout,
            1,
            line.Bounds.X + 5.5 + 1,
            line.Bounds.Y + 1);

        insideEmoji.ShouldNotBeNull();
        insideEmoji!.Value.ParagraphOffset.ShouldBe(1);

        var afterEmoji = LayoutHitTesting.HitTestLine(
            layout,
            1,
            line.Bounds.X + 5.5 + 4,
            line.Bounds.Y + 1);

        afterEmoji.ShouldNotBeNull();
        afterEmoji!.Value.ParagraphOffset.ShouldBe(3);
    }

    [Fact]
    public void Layout_hit_test_in_margin_uses_nearest_line()
    {
        var document = CreateDocument("Hello\nworld");
        var layout = new LayoutEngine().Layout(document);
        var lines = layout.Pages[0].Paragraphs.Single().Lines;

        var hit = LayoutHitTesting.HitTestLine(
            layout,
            1,
            x: 0,
            y: lines[1].Bounds.Y + 1);

        hit.ShouldNotBeNull();
        hit!.Value.LineIndex.ShouldBe(1);
        hit!.Value.ParagraphOffset.ShouldBe(lines[1].TextStart);
    }

    [Fact]
    public void Selection_geometry_can_span_multiple_pages()
    {
        var text = string.Join(' ', Enumerable.Repeat("word", 2000));
        var document = CreateDocument(text);
        var layout = new LayoutEngine().Layout(document);
        var paragraph = layout.Pages[0].Paragraphs[0];

        var start = LayoutHitTesting.FindCaret(
            layout,
            paragraph.ParagraphId,
            0,
            TextAffinity.Downstream);
        var end = LayoutHitTesting.FindCaret(
            layout,
            paragraph.ParagraphId,
            text.Length,
            TextAffinity.Upstream);

        start.ShouldNotBeNull();
        end.ShouldNotBeNull();
        start.Page.PageNumber.ShouldBe(1);
        end.Page.PageNumber.ShouldBe(3);
    }
    private static Document CreateDocument(string text)
    {
        var paragraph = ParagraphBlock.Create(text);
        var section = new Section(
            Guid.NewGuid(),
            ImmutableArray.Create<Block>(paragraph));

        return new Document(
            Guid.NewGuid(),
            "Test",
            ImmutableArray.Create(section));
    }
}
