using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using MicroslopWords.Layout;
using Shouldly;

namespace MicroslopWords.Layout.Tests;

public sealed class LayoutParagraphTests
{
    [Fact]
    public void Layout_centers_and_right_aligns_lines()
    {
        var center = CreateParagraph("Hi", new ParagraphProperties
        {
            Alignment = ParagraphAlignment.Center,
        });
        var right = CreateParagraph("Hi", new ParagraphProperties
        {
            Alignment = ParagraphAlignment.Right,
        });
        var layout = new LayoutEngine().Layout(CreateDocument(center, right));
        var centerLine = layout.Pages[0].Paragraphs[0].Lines[0];
        var rightLine = layout.Pages[0].Paragraphs[1].Lines[0];

        centerLine.Bounds.X.ShouldBeGreaterThan(72);
        rightLine.Bounds.X.ShouldBeGreaterThan(centerLine.Bounds.X);
    }

    [Fact]
    public void Layout_applies_left_right_and_first_line_indents()
    {
        var paragraph = CreateParagraph(
            string.Join(' ', Enumerable.Repeat("word", 200)),
            new ParagraphProperties
            {
                LeftIndentPoints = 36,
                RightIndentPoints = 36,
                FirstLineIndentPoints = 18,
            });
        var layout = new LayoutEngine().Layout(CreateDocument(paragraph));
        var lines = layout.Pages[0].Paragraphs[0].Lines;

        lines.Length.ShouldBeGreaterThan(1);
        lines[0].Bounds.X.ShouldBe(72 + 36 + 18);
        lines[1].Bounds.X.ShouldBe(72 + 36);
        lines.ShouldAllBe(line => line.Bounds.Width <= 468 - 72 + 0.0001);
    }

    [Fact]
    public void Layout_justifies_non_final_lines_to_the_full_width()
    {
        var paragraph = CreateParagraph(
            string.Join(' ', Enumerable.Repeat("word", 200)),
            new ParagraphProperties
            {
                Alignment = ParagraphAlignment.Justify,
            });
        var layout = new LayoutEngine().Layout(CreateDocument(paragraph));
        var lines = layout.Pages[0].Paragraphs[0].Lines;

        lines.Length.ShouldBeGreaterThan(1);
        lines[0].Bounds.Width.ShouldBe(468, 0.0001);
        lines[^1].Bounds.Width.ShouldBeLessThan(468);
    }

    [Fact]
    public void Layout_emits_list_labels_and_nested_indentation()
    {
        var listId = Guid.NewGuid();
        var first = CreateParagraph("First", new ParagraphProperties
        {
            ListKind = ListKind.Bulleted,
            ListLevel = 1,
            ListId = listId,
        });
        var second = CreateParagraph("Second", new ParagraphProperties
        {
            ListKind = ListKind.Numbered,
            ListLevel = 2,
            ListId = listId,
        });

        var layout = new LayoutEngine().Layout(CreateDocument(first, second));
        var paragraphs = layout.Pages[0].Paragraphs;

        paragraphs[0].ListLabel.ShouldBe("\u2022");
        paragraphs[0].ListLabelX.ShouldBe(72);
        paragraphs[0].Lines[0].Bounds.X.ShouldBe(90);
        paragraphs[1].ListLabel.ShouldBe("1.");
        paragraphs[1].ListLabelX.ShouldBe(90);
        paragraphs[1].Lines[0].Bounds.X.ShouldBe(108);
    }

    [Fact]
    public void Layout_numbering_continues_and_restarts_with_list_identity()
    {
        var firstId = Guid.NewGuid();
        var first = CreateParagraph("One", Numbered(firstId));
        var second = CreateParagraph("Two", Numbered(firstId));
        var restarted = CreateParagraph("Again", Numbered(Guid.NewGuid()));

        var layout = new LayoutEngine().Layout(
            CreateDocument(first, second, restarted));
        var paragraphs = layout.Pages[0].Paragraphs;

        paragraphs[0].ListLabel.ShouldBe("1.");
        paragraphs[1].ListLabel.ShouldBe("2.");
        paragraphs[2].ListLabel.ShouldBe("1.");
    }

    [Fact]
    public void Layout_page_break_starts_a_new_page_and_records_the_marker()
    {
        var paragraph = CreateParagraph("Before\fAfter");
        var layout = new LayoutEngine().Layout(CreateDocument(paragraph));

        layout.Pages.Length.ShouldBe(2);
        layout.Pages[0].Paragraphs.Single().Lines.Single().Text.ShouldBe("Before");
        layout.Pages[1].Paragraphs.Single().Lines.Single().Text.ShouldBe("After");
        layout.PageBreaks.Length.ShouldBe(1);
        layout.PageBreaks[0].ParagraphId.ShouldBe(paragraph.Id);
        layout.PageBreaks[0].TextOffset.ShouldBe(6);
        layout.PageBreaks[0].PageNumber.ShouldBe(1);
    }

    [Fact]
    public void Layout_page_break_at_the_end_does_not_create_a_blank_page()
    {
        var paragraph = CreateParagraph("After\f");
        var layout = new LayoutEngine().Layout(CreateDocument(paragraph));

        layout.Pages.Length.ShouldBe(1);
        layout.Pages[0].Paragraphs.Single().Lines.Single().Text.ShouldBe("After");
        layout.PageBreaks.Length.ShouldBe(1);
    }

    [Fact]
    public void Layout_line_spacing_and_space_before_affect_page_geometry()
    {
        var first = CreateParagraph("First", new ParagraphProperties
        {
            SpaceBeforePoints = 20,
            LineSpacing = 2,
        });
        var second = CreateParagraph("Second");

        var layout = new LayoutEngine().Layout(CreateDocument(first, second));
        var paragraphs = layout.Pages[0].Paragraphs;

        paragraphs[0].Lines[0].Bounds.Y.ShouldBe(92);
        paragraphs[0].Lines[0].Bounds.Height.ShouldBe(22);
        paragraphs[1].Lines[0].Bounds.Y.ShouldBe(92 + 22 + 8);
    }

    private static ParagraphProperties Numbered(Guid listId) => new()
    {
        ListKind = ListKind.Numbered,
        ListLevel = 1,
        ListId = listId,
    };

    private static ParagraphBlock CreateParagraph(
        string text,
        ParagraphProperties? properties = null) =>
        new(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.Create(text)),
            properties);

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
}
