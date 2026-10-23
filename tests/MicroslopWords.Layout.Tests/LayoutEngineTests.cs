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

    [Fact]
    public void Layout_records_substitution_diagnostic_when_font_catalog_is_empty()
    {
        var document = CreateDocument("Hello world");

        var layout = new LayoutEngine().Layout(document);

        layout.Diagnostics.ShouldContain(diagnostic =>
            diagnostic.Code == LayoutDiagnosticCodes.FontSubstituted);
    }

    [Fact]
    public void Layout_is_deterministic_for_the_same_document_and_catalog()
    {
        var document = CreateDocument(string.Join(' ', Enumerable.Repeat("word", 200)));
        var engine = new LayoutEngine();
        var catalog = LoadSystemCatalog();

        if (catalog is null)
        {
            return;
        }

        var first = engine.Layout(document, catalog);
        var second = engine.Layout(document, catalog);

        first.Pages.Length.ShouldBe(second.Pages.Length);

        for (var page = 0; page < first.Pages.Length; page++)
        {
            var firstLines = first.Pages[page].Paragraphs.SelectMany(p => p.Lines).ToList();
            var secondLines = second.Pages[page].Paragraphs.SelectMany(p => p.Lines).ToList();

            firstLines.Count.ShouldBe(secondLines.Count);

            for (var line = 0; line < firstLines.Count; line++)
            {
                firstLines[line].Text.ShouldBe(secondLines[line].Text);
                firstLines[line].Bounds.ShouldBe(secondLines[line].Bounds);
                firstLines[line].ResolvedFont.ShouldBe(secondLines[line].ResolvedFont);
            }
        }
    }

    [Fact]
    public void Layout_produces_positive_line_widths_with_a_font_catalog()
    {
        var bytes = TestFonts.TryLoadBytes();

        if (bytes is null)
        {
            return;
        }

        using var face = new FontFace("Test", FontWeight.Regular, FontSlant.Upright, bytes);
        var catalog = FontCatalog.Create([face]);
        var document = CreateDocument("Hello world");

        var layout = new LayoutEngine().Layout(document, catalog);
        var line = layout.Pages[0].Paragraphs.SelectMany(paragraph => paragraph.Lines).Single();

        line.Bounds.Width.ShouldBeGreaterThan(0);
        line.ResolvedFont.ShouldBe(face.Key);
    }

    private static FontCatalog? LoadSystemCatalog()
    {
        var bytes = TestFonts.TryLoadBytes();

        return bytes is null
            ? null
            : FontCatalog.Create(
                [new FontFace("Test", FontWeight.Regular, FontSlant.Upright, bytes)]);
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
