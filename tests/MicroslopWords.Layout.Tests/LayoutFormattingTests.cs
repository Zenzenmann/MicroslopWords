using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using MicroslopWords.Layout;
using Shouldly;

namespace MicroslopWords.Layout.Tests;

public sealed class LayoutFormattingTests
{
    [Fact]
    public void Layout_uses_per_run_font_size_and_emits_styled_spans()
    {
        var small = Run.Create("small ", new RunProperties { FontSizePoints = 10 });
        var large = Run.Create("BIG", new RunProperties { FontSizePoints = 24 });
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(small, large));
        var document = CreateDocument(paragraph);

        var layout = new LayoutEngine().Layout(document);
        var line = layout.Pages[0].Paragraphs.Single().Lines.Single();

        line.FontSizePoints.ShouldBe(24);
        line.Bounds.Height.ShouldBe(27.6, 0.0001);
        line.Spans.Length.ShouldBe(2);
        line.Spans[0].FontSizePoints.ShouldBe(10);
        line.Spans[1].FontSizePoints.ShouldBe(24);
        line.Spans[1].X.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Layout_carries_bold_italic_color_highlight_and_decoration_into_spans()
    {
        var run = Run.Create(
            "Styled",
            new RunProperties
            {
                FontFamily = "MissingFont",
                Bold = true,
                Italic = true,
                Underline = true,
                Strikethrough = true,
                ColorHex = "#FF0000",
                HighlightHex = "#FFFF00",
                FontSizePoints = 14,
            });
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(run));

        var layout = new LayoutEngine().Layout(CreateDocument(paragraph));
        var span = layout.Pages[0].Paragraphs.Single().Lines.Single().Spans.Single();

        span.Font.Family.ShouldBe("MissingFont");
        span.Font.Weight.ShouldBe(FontWeight.Bold);
        span.Font.Slant.ShouldBe(FontSlant.Italic);
        span.FontSizePoints.ShouldBe(14);
        span.ColorHex.ShouldBe("#FF0000");
        span.HighlightHex.ShouldBe("#FFFF00");
        span.Underline.ShouldBeTrue();
        span.Strikethrough.ShouldBeTrue();
        layout.Diagnostics.ShouldContain(diagnostic =>
            diagnostic.Code == LayoutDiagnosticCodes.FontSubstituted
            && diagnostic.Message.Contains("MissingFont"));
    }

    [Fact]
    public void Layout_splits_runs_at_soft_break_segments()
    {
        var first = Run.Create("a", new RunProperties { Bold = true });
        var second = Run.Create("b\nc", new RunProperties { Italic = true });
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(first, second));

        var layout = new LayoutEngine().Layout(CreateDocument(paragraph));
        var lines = layout.Pages[0].Paragraphs.Single().Lines;

        lines.Length.ShouldBe(2);
        lines[0].Spans[0].Font.Weight.ShouldBe(FontWeight.Bold);
        lines[0].Spans.Length.ShouldBe(2);
        lines[1].Spans.Single().Font.Slant.ShouldBe(FontSlant.Italic);
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
