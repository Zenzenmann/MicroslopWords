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
        using var face = LoadSystemFontFace();

        if (face is null)
        {
            return;
        }

        var catalog = FontCatalog.Create([face]);
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
                firstLines[line].Clusters.ShouldBe(secondLines[line].Clusters);
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

    [Fact]
    public void Layout_uses_section_page_size_and_orientation()
    {
        var document = CreateDocument(
            string.Join(' ', Enumerable.Repeat("word", 200)),
            PageSettings.A4 with { Orientation = PageOrientation.Landscape });

        var layout = new LayoutEngine().Layout(document);

        layout.PageWidthPoints.ShouldBe(841.89);
        layout.PageHeightPoints.ShouldBe(595.28);
        layout.Pages[0].Bounds.Width.ShouldBe(841.89);
        layout.Pages[0].Bounds.Height.ShouldBe(595.28);
    }

    [Fact]
    public void Layout_respects_paragraph_spacing_and_line_spacing()
    {
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.Create("Spaced")),
            new ParagraphProperties
            {
                SpaceBeforePoints = 10,
                LineSpacing = 2,
            });
        var document = CreateDocument(paragraph);

        var layout = new LayoutEngine().Layout(document);
        var line = layout.Pages[0].Paragraphs.SelectMany(item => item.Lines).Single();

        line.Bounds.Y.ShouldBe(82);
        line.Bounds.Height.ShouldBe(22);
    }

    [Fact]
    public void Layout_lines_stay_inside_page_bounds_and_do_not_overlap()
    {
        var document = CreateDocument(string.Join(' ', Enumerable.Repeat("word", 1500)));
        var layout = new LayoutEngine().Layout(document);
        var pageIndex = 0;

        foreach (var page in layout.Pages)
        {
            var lines = page.Paragraphs.SelectMany(paragraph => paragraph.Lines).ToList();

            lines.ShouldNotBeEmpty($"page {pageIndex + 1} should contain lines");

            foreach (var line in lines)
            {
                line.Bounds.Y.ShouldBeGreaterThanOrEqualTo(72);
                line.Bounds.Bottom.ShouldBeLessThanOrEqualTo(page.Bounds.Height - 72 + 0.0001);
            }

            for (var index = 1; index < lines.Count; index++)
            {
                lines[index].Bounds.Y.ShouldBeGreaterThanOrEqualTo(
                    lines[index - 1].Bounds.Bottom - 0.0001);
            }

            pageIndex++;
        }
    }

    [Fact]
    public void Layout_matches_golden_page_geometry_for_fixed_input()
    {
        var document = CreateDocument(
            string.Join(' ', Enumerable.Repeat("word", 2000)));

        var layout = new LayoutEngine().Layout(document);

        layout.PageWidthPoints.ShouldBe(612);
        layout.PageHeightPoints.ShouldBe(792);
        layout.Pages.Length.ShouldBe(3);
        layout.Pages
            .Select(page => page.Paragraphs.SelectMany(paragraph => paragraph.Lines).Count())
            .ShouldBe([51, 51, 16]);

        var firstLine = layout.Pages[0].Paragraphs.Single().Lines[0];

        firstLine.Text.ShouldBe(
            string.Join(' ', Enumerable.Repeat("word", 17)) + " ");
        firstLine.Bounds.X.ShouldBe(72);
        firstLine.Bounds.Y.ShouldBe(72);
        firstLine.Bounds.Width.ShouldBe(467.5, 0.0001);
        firstLine.Bounds.Height.ShouldBe(12.65, 0.0001);
    }

    [Fact]
    public void Layout_empty_document_produces_exactly_one_page()
    {
        var layout = new LayoutEngine().Layout(Document.CreateEmpty());

        layout.Pages.Length.ShouldBe(1);
        layout.Pages[0].Paragraphs.Length.ShouldBe(1);
    }

    private static FontFace? LoadSystemFontFace()
    {
        var bytes = TestFonts.TryLoadBytes();

        return bytes is null
            ? null
            : new FontFace("Test", FontWeight.Regular, FontSlant.Upright, bytes);
    }

    private static Document CreateDocument(string text) =>
        CreateDocument(
            new ParagraphBlock(
                Guid.NewGuid(),
                ImmutableArray.Create(Run.Create(text))));

    private static Document CreateDocument(ParagraphBlock paragraph) =>
        CreateDocument(paragraph, page: null);

    private static Document CreateDocument(string text, PageSettings page) =>
        CreateDocument(
            new ParagraphBlock(
                Guid.NewGuid(),
                ImmutableArray.Create(Run.Create(text))),
            page);

    private static Document CreateDocument(ParagraphBlock paragraph, PageSettings? page)
    {
        var section = new Section(
            Guid.NewGuid(),
            ImmutableArray.Create<Block>(paragraph),
            page);

        return new Document(Guid.NewGuid(), "Test", ImmutableArray.Create(section));
    }
}
