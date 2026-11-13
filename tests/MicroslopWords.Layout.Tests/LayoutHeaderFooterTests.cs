using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using Shouldly;

namespace MicroslopWords.Layout.Tests;

public sealed class LayoutHeaderFooterTests
{
    [Fact]
    public void Layout_renders_header_and_footer_on_every_page_within_margins()
    {
        var header = CreateTextHeader("Header");
        var footer = CreateTextHeader("Footer");
        var section = CreateSection(
            header: header,
            footer: footer,
            bodyParagraphs: 70);
        var document = new Document(
            Guid.NewGuid(),
            "Header",
            ImmutableArray.Create(section));

        var layout = new LayoutEngine().Layout(document);

        layout.Pages.Length.ShouldBeGreaterThan(1);
        layout.Pages.ShouldAllBe(page => page.Header != null);
        layout.Pages.ShouldAllBe(page => page.Footer != null);

        foreach (var page in layout.Pages)
        {
            var pageHeader = page.Header.ShouldNotBeNull();
            var pageFooter = page.Footer.ShouldNotBeNull();

            pageHeader.Bounds.Y.ShouldBeGreaterThanOrEqualTo(0);
            pageHeader.Bounds.Bottom.ShouldBeLessThanOrEqualTo(page.MarginTopPoints);
            pageFooter.Bounds.Y.ShouldBeGreaterThanOrEqualTo(
                page.Bounds.Height - page.MarginBottomPoints);
            pageFooter.Bounds.Bottom.ShouldBeLessThanOrEqualTo(page.Bounds.Height);
        }
    }

    [Fact]
    public void Layout_uses_first_page_variant_only_on_the_first_page()
    {
        var defaultHeader = CreateTextHeader("Default header");
        var firstHeader = CreateTextHeader("First page header");
        var section = CreateSection(
            header: defaultHeader,
            firstPageHeader: firstHeader,
            differentFirstPage: true,
            bodyParagraphs: 70);
        var document = new Document(
            Guid.NewGuid(),
            "First page",
            ImmutableArray.Create(section));

        var layout = new LayoutEngine().Layout(document);

        layout.Pages.Length.ShouldBeGreaterThan(1);
        HeaderText(layout.Pages[0]).ShouldBe("First page header");
        HeaderText(layout.Pages[1]).ShouldBe("Default header");
    }

    [Fact]
    public void Layout_resolves_page_and_total_page_fields()
    {
        var header = new HeaderFooter(
            ImmutableArray.Create<Block>(new ParagraphBlock(
                Guid.NewGuid(),
                ImmutableArray.Create(
                    Run.Create("Page "),
                    Run.CreatePageField(PageFieldKind.PageNumber),
                    Run.Create(" of "),
                    Run.CreatePageField(PageFieldKind.TotalPages)))));
        var section = CreateSection(
            header: header,
            bodyParagraphs: 70);
        var document = new Document(
            Guid.NewGuid(),
            "Fields",
            ImmutableArray.Create(section));

        var layout = new LayoutEngine().Layout(document);

        layout.Pages.Length.ShouldBeGreaterThan(1);
        HeaderText(layout.Pages[0]).ShouldBe($"Page 1 of {layout.Pages.Length}");
        HeaderText(layout.Pages[1]).ShouldBe($"Page 2 of {layout.Pages.Length}");
    }

    [Fact]
    public void Layout_applies_page_number_start_and_format()
    {
        var header = new HeaderFooter(
            ImmutableArray.Create<Block>(new ParagraphBlock(
                Guid.NewGuid(),
                ImmutableArray.Create(Run.CreatePageField(PageFieldKind.PageNumber)))));
        var section = CreateSection(
            header: header,
            bodyParagraphs: 70,
            pageNumberStart: 5,
            pageNumberFormat: PageNumberFormat.UpperRoman);
        var document = new Document(
            Guid.NewGuid(),
            "Roman",
            ImmutableArray.Create(section));

        var layout = new LayoutEngine().Layout(document);

        HeaderText(layout.Pages[0]).ShouldBe("V");
        HeaderText(layout.Pages[1]).ShouldBe("VI");
    }

    [Fact]
    public void Layout_reports_overflowing_header_content()
    {
        var headerBlocks = ImmutableArray.CreateBuilder<Block>();

        for (var index = 0; index < 12; index++)
        {
            headerBlocks.Add(ParagraphBlock.Create($"Header line {index}"));
        }

        var section = CreateSection(
            header: new HeaderFooter(headerBlocks.ToImmutable()),
            bodyParagraphs: 1);
        var document = new Document(
            Guid.NewGuid(),
            "Overflow",
            ImmutableArray.Create(section));

        var layout = new LayoutEngine().Layout(document);

        layout.Diagnostics.ShouldContain(diagnostic =>
            diagnostic.Code == LayoutDiagnosticCodes.HeaderFooterOverflow
            && diagnostic.Severity == Core.Diagnostics.DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Layout_leaves_header_and_footer_empty_when_not_defined()
    {
        var document = Document.CreateEmpty();

        var layout = new LayoutEngine().Layout(document);

        layout.Pages.ShouldHaveSingleItem().Header.ShouldBeNull();
        layout.Pages.ShouldHaveSingleItem().Footer.ShouldBeNull();
    }

    private static Section CreateSection(
        HeaderFooter? header = null,
        HeaderFooter? footer = null,
        HeaderFooter? firstPageHeader = null,
        bool differentFirstPage = false,
        int bodyParagraphs = 1,
        int? pageNumberStart = null,
        PageNumberFormat? pageNumberFormat = null)
    {
        var blocks = ImmutableArray.CreateBuilder<Block>();

        for (var index = 0; index < bodyParagraphs; index++)
        {
            blocks.Add(ParagraphBlock.Create($"Body paragraph {index}"));
        }

        return Section.CreateEmpty() with
        {
            Blocks = blocks.ToImmutable(),
            Header = header,
            Footer = footer,
            FirstPageHeader = firstPageHeader,
            DifferentFirstPage = differentFirstPage,
            PageNumberStart = pageNumberStart,
            PageNumberFormat = pageNumberFormat,
        };
    }

    private static HeaderFooter CreateTextHeader(string text) =>
        new(ImmutableArray.Create<Block>(ParagraphBlock.Create(text)));

    private static string HeaderText(PageLayout page) =>
        string.Join(
            " ",
            page.Header.ShouldNotBeNull()
                .Paragraphs
                .SelectMany(paragraph => paragraph.Lines)
                .Select(line => line.Text));
}
