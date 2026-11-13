using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using Shouldly;

namespace MicroslopWords.Core.Tests;

public sealed class HeaderFooterTests
{
    [Theory]
    [InlineData(1, PageNumberFormat.DecimalNumber, "1")]
    [InlineData(4, PageNumberFormat.UpperRoman, "IV")]
    [InlineData(9, PageNumberFormat.LowerRoman, "ix")]
    [InlineData(1, PageNumberFormat.UpperAlphabetic, "A")]
    [InlineData(26, PageNumberFormat.LowerAlphabetic, "z")]
    [InlineData(27, PageNumberFormat.LowerAlphabetic, "aa")]
    public void PageNumberFormatter_formats_supported_formats(
        int number,
        PageNumberFormat format,
        string expected)
    {
        PageNumberFormatter.Format(number, format).ShouldBe(expected);
    }

    [Fact]
    public void Document_resolves_linked_header_and_footer_from_previous_section()
    {
        var header = HeaderFooter.CreateEmpty();
        var footer = HeaderFooter.CreateEmpty();
        var first = Section.CreateEmpty() with
        {
            Header = header,
            Footer = footer,
        };
        var second = Section.CreateEmpty() with
        {
            LinkHeaderToPrevious = true,
            LinkFooterToPrevious = true,
        };
        var document = new Document(
            Guid.NewGuid(),
            "Linked",
            ImmutableArray.Create(first, second));

        document.ResolveHeaderFooter(1, HeaderFooterKind.Header, HeaderFooterVariant.Default)
            .ShouldBe(header);
        document.ResolveHeaderFooter(1, HeaderFooterKind.Footer, HeaderFooterVariant.Default)
            .ShouldBe(footer);
        document.FindHeaderFooterOwnerIndex(1, HeaderFooterKind.Header).ShouldBe(0);
    }

    [Fact]
    public void EnterHeaderFooterCommand_creates_header_and_selects_first_paragraph()
    {
        var document = Document.CreateEmpty();
        var section = document.Sections.Single();
        var command = new EnterHeaderFooterCommand(
            section.Id,
            HeaderFooterKind.Header,
            HeaderFooterVariant.Default);

        var updated = command.Apply(document);
        var updatedSection = updated.Sections.Single();
        var header = updatedSection.Header.ShouldNotBeNull();

        header.Blocks.OfType<ParagraphBlock>().ShouldHaveSingleItem();
        updated.FindHeaderFooterZone(
            command.SelectionAfter.Focus.ParagraphId).ShouldNotBeNull();
        updated.FindHeaderFooterZone(
            command.SelectionAfter.Focus.ParagraphId)!.Value.Kind
            .ShouldBe(HeaderFooterKind.Header);
    }

    [Fact]
    public void InsertPageFieldCommand_inserts_page_and_total_fields()
    {
        var (document, headerParagraph) = CreateHeaderDocument();
        var selection = Selection.Caret(TextNavigation.StartOf(headerParagraph));

        var withPage = new InsertPageFieldCommand(
            selection,
            PageFieldKind.PageNumber).Apply(document);
        var pageRuns = withPage.Sections.Single().Header!
            .Blocks.OfType<ParagraphBlock>().Single().Runs;

        pageRuns.ShouldContain(run => run.PageField == PageFieldKind.PageNumber);

        var pageSelection = Selection.Caret(new TextPosition(
            headerParagraph.Id,
            pageRuns.First(run => run.PageField == PageFieldKind.PageNumber).Id,
            0));
        var withTotal = new InsertPageFieldCommand(
            pageSelection,
            PageFieldKind.TotalPages).Apply(withPage);
        var totalRuns = withTotal.Sections.Single().Header!
            .Blocks.OfType<ParagraphBlock>().Single().Runs;

        totalRuns.ShouldContain(run => run.PageField == PageFieldKind.PageNumber);
        totalRuns.ShouldContain(run => run.PageField == PageFieldKind.TotalPages);
    }

    [Fact]
    public void InsertPageFieldCommand_rejects_body_paragraphs()
    {
        var document = Document.CreateEmpty();
        var bodyParagraph = document.EnumerateParagraphs().Single();
        var command = new InsertPageFieldCommand(
            Selection.Caret(TextNavigation.StartOf(bodyParagraph)),
            PageFieldKind.PageNumber);

        Should.Throw<InvalidOperationException>(() => command.Apply(document));
    }

    [Fact]
    public void Editing_text_inside_header_does_not_change_body()
    {
        var (document, headerParagraph) = CreateHeaderDocument();
        var selection = Selection.Caret(TextNavigation.StartOf(headerParagraph));

        var updated = new InsertTextAtSelectionCommand(selection, "Draft").Apply(document);

        updated.Sections.Single().Header!.PlainText.ShouldBe("Draft");
        updated.PlainText.ShouldBeEmpty();
        updated.EnumerateParagraphs().ShouldHaveSingleItem();
    }

    [Fact]
    public void SetHeaderFooterLinkCommand_unlinks_to_an_independent_copy()
    {
        var header = HeaderFooter.CreateEmpty();
        var first = Section.CreateEmpty() with { Header = header };
        var second = Section.CreateEmpty() with { LinkHeaderToPrevious = true };
        var document = new Document(
            Guid.NewGuid(),
            "Linked",
            ImmutableArray.Create(first, second));
        var headerParagraph = header.Blocks.OfType<ParagraphBlock>().Single();
        var selection = Selection.Caret(TextNavigation.StartOf(headerParagraph));

        var unlinked = new SetHeaderFooterLinkCommand(
            selection,
            second.Id,
            HeaderFooterKind.Header,
            linked: false).Apply(document);
        var secondSection = unlinked.Sections[1];

        secondSection.LinkHeaderToPrevious.ShouldBeFalse();
        secondSection.Header.ShouldNotBeNull();
        secondSection.Header!.PlainText.ShouldBe(header.PlainText);
        secondSection.Header.Blocks[0].Id.ShouldNotBe(header.Blocks[0].Id);

        var relinked = new SetHeaderFooterLinkCommand(
            selection,
            second.Id,
            HeaderFooterKind.Header,
            linked: true).Apply(unlinked);

        relinked.Sections[1].LinkHeaderToPrevious.ShouldBeTrue();
        relinked.Sections[1].Header.ShouldBeNull();
    }

    [Fact]
    public void SetHeaderFooterLinkCommand_rejects_linking_the_first_section()
    {
        var document = Document.CreateEmpty();
        var section = document.Sections.Single();
        var paragraph = document.EnumerateParagraphs().Single();
        var command = new SetHeaderFooterLinkCommand(
            Selection.Caret(TextNavigation.StartOf(paragraph)),
            section.Id,
            HeaderFooterKind.Header,
            linked: true);

        Should.Throw<InvalidOperationException>(() => command.Apply(document));
    }

    [Fact]
    public void Page_number_commands_persist_section_values_and_preserve_selection()
    {
        var (document, headerParagraph) = CreateHeaderDocument();
        var section = document.Sections.Single();
        var selection = Selection.Caret(TextNavigation.StartOf(headerParagraph));

        var formatted = new SetPageNumberFormatCommand(
            selection,
            section.Id,
            PageNumberFormat.UpperRoman).Apply(document);
        var started = new SetPageNumberStartCommand(
            selection,
            section.Id,
            3).Apply(formatted);

        started.Sections.Single().PageNumberFormat.ShouldBe(PageNumberFormat.UpperRoman);
        started.Sections.Single().PageNumberStart.ShouldBe(3);
    }

    [Fact]
    public void Different_first_page_command_updates_section_flag()
    {
        var (document, headerParagraph) = CreateHeaderDocument();
        var section = document.Sections.Single();
        var selection = Selection.Caret(TextNavigation.StartOf(headerParagraph));

        var updated = new SetDifferentFirstPageCommand(
            selection,
            section.Id,
            enabled: true).Apply(document);

        updated.Sections.Single().DifferentFirstPage.ShouldBeTrue();
    }

    private static (Document Document, ParagraphBlock HeaderParagraph)
        CreateHeaderDocument()
    {
        var header = HeaderFooter.CreateEmpty();
        var section = Section.CreateEmpty() with { Header = header };
        var document = new Document(
            Guid.NewGuid(),
            "Header",
            ImmutableArray.Create(section));

        return (document, header.Blocks.OfType<ParagraphBlock>().Single());
    }
}
