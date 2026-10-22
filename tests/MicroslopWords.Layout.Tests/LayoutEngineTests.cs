using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using Shouldly;

namespace MicroslopWords.Layout.Tests;

public sealed class LayoutEngineTests
{
    [Fact]
    public void Layout_short_paragraph_produces_single_line_on_first_page()
    {
        var document = CreateDocument("Hello world");

        var layout = new LayoutEngine().Layout(document);
        var lines = layout.Pages[0].Paragraphs.SelectMany(paragraph => paragraph.Lines).ToList();

        layout.Pages.Length.ShouldBe(1);
        lines.Count.ShouldBe(1);
        lines[0].Text.ShouldBe("Hello world");
        lines[0].Bounds.X.ShouldBe(72);
        lines[0].Bounds.Y.ShouldBe(72);
    }

    [Fact]
    public void Layout_long_paragraph_wraps_and_paginates()
    {
        var text = string.Join(' ', Enumerable.Repeat("word", 1500));
        var document = CreateDocument(text);

        var layout = new LayoutEngine().Layout(document);
        var lineCount = layout.Pages
            .SelectMany(page => page.Paragraphs)
            .SelectMany(paragraph => paragraph.Lines)
            .Count();

        var details = $"text={text.Length} lines={lineCount} pages={layout.Pages.Length} paragraphsOnPage1={layout.Pages[0].Paragraphs.Length}";

        layout.Pages.Length.ShouldBeGreaterThan(1, details);
        lineCount.ShouldBeGreaterThan(1, details);
    }

    private static Document CreateDocument(string text)
    {
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.Create(text)));

        var section = new Section(
            Guid.NewGuid(),
            ImmutableArray.Create<Block>(paragraph));

        return new Document(Guid.NewGuid(), "Test", ImmutableArray.Create(section));
    }
}
