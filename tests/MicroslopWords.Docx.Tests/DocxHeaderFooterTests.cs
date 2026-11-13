using System.Collections.Immutable;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using MicroslopWords.Core.Documents;
using MicroslopWords.Formats.Docx;
using Shouldly;

namespace MicroslopWords.Docx.Tests;

public sealed class DocxHeaderFooterTests
{
    [Fact]
    public void SaveAndLoad_round_trips_header_footer_and_page_fields()
    {
        var header = new HeaderFooter(
            ImmutableArray.Create<Block>(ParagraphBlock.Create("Report header")));
        var footer = new HeaderFooter(
            ImmutableArray.Create<Block>(new ParagraphBlock(
                Guid.NewGuid(),
                ImmutableArray.Create(
                    Run.Create("Page "),
                    Run.CreatePageField(PageFieldKind.PageNumber),
                    Run.Create(" of "),
                    Run.CreatePageField(PageFieldKind.TotalPages)))));
        var section = Section.CreateEmpty() with
        {
            Header = header,
            Footer = footer,
        };
        var document = CreateDocument(section);

        using var stream = SaveAndValidate(document);
        var loaded = DocxService.Load(
            stream,
            "Headers",
            TestContext.Current.CancellationToken);

        loaded.IsSuccess.ShouldBeTrue();
        var loadedSection = loaded.Value!.Sections.Single();

        loadedSection.Header.ShouldNotBeNull();
        loadedSection.Header!.PlainText.ShouldBe("Report header");
        loadedSection.Footer.ShouldNotBeNull();

        var footerRuns = loadedSection.Footer!
            .Blocks.OfType<ParagraphBlock>()
            .Single()
            .Runs;

        footerRuns.ShouldContain(run => run.PageField == PageFieldKind.PageNumber);
        footerRuns.ShouldContain(run => run.PageField == PageFieldKind.TotalPages);
    }

    [Fact]
    public void SaveAndLoad_round_trips_first_page_variant_and_page_number_settings()
    {
        var section = Section.CreateEmpty() with
        {
            Header = new HeaderFooter(
                ImmutableArray.Create<Block>(ParagraphBlock.Create("Default header"))),
            FirstPageHeader = new HeaderFooter(
                ImmutableArray.Create<Block>(ParagraphBlock.Create("First header"))),
            DifferentFirstPage = true,
            PageNumberStart = 3,
            PageNumberFormat = PageNumberFormat.LowerRoman,
        };
        var document = CreateDocument(section);

        using var stream = SaveAndValidate(document);
        var loaded = DocxService.Load(
            stream,
            "First page",
            TestContext.Current.CancellationToken);
        var loadedSection = loaded.Value!.Sections.Single();

        loadedSection.DifferentFirstPage.ShouldBeTrue();
        loadedSection.Header!.PlainText.ShouldBe("Default header");
        loadedSection.FirstPageHeader!.PlainText.ShouldBe("First header");
        loadedSection.PageNumberStart.ShouldBe(3);
        loadedSection.PageNumberFormat.ShouldBe(PageNumberFormat.LowerRoman);
    }

    [Fact]
    public void SaveAndLoad_round_trips_header_images()
    {
        var image = new InlineImage(
            ImageFormat.Png,
            "logo.png",
            ImmutableArray.Create(ImageTestData.Png()),
            100,
            50,
            75,
            37.5,
            "Logo");
        var header = new HeaderFooter(
            ImmutableArray.Create<Block>(new ParagraphBlock(
                Guid.NewGuid(),
                ImmutableArray.Create(Run.CreateImage(image)))));
        var section = Section.CreateEmpty() with { Header = header };
        var document = CreateDocument(section);

        using var stream = SaveAndValidate(document);
        var loaded = DocxService.Load(
            stream,
            "Header image",
            TestContext.Current.CancellationToken);
        var loadedImage = loaded.Value!.Sections.Single().Header!
            .Blocks.OfType<ParagraphBlock>()
            .Single()
            .Runs
            .Select(run => run.EffectiveImage)
            .Single(imageValue => imageValue is not null);

        loadedImage.ShouldNotBeNull();
        loadedImage!.Data.ToArray().ShouldBe(ImageTestData.Png());
        loadedImage.AltText.ShouldBe("Logo");
    }

    [Fact]
    public void SaveAndLoad_round_trips_linked_sections()
    {
        var first = Section.CreateEmpty() with
        {
            Header = new HeaderFooter(
                ImmutableArray.Create<Block>(ParagraphBlock.Create("Shared header"))),
        };
        var second = Section.CreateEmpty() with
        {
            LinkHeaderToPrevious = true,
            Blocks = ImmutableArray.Create<Block>(ParagraphBlock.Create("Second section")),
        };
        var document = new Document(
            Guid.NewGuid(),
            "Sections",
            ImmutableArray.Create(first, second));

        using var stream = SaveAndValidate(document);
        var loaded = DocxService.Load(
            stream,
            "Sections",
            TestContext.Current.CancellationToken);
        var loadedDocument = loaded.Value!;

        loadedDocument.Sections.Length.ShouldBe(2);
        loadedDocument.Sections[1].LinkHeaderToPrevious.ShouldBeTrue();
        loadedDocument.ResolveHeaderFooter(
                1,
                HeaderFooterKind.Header,
                HeaderFooterVariant.Default)!
            .PlainText.ShouldBe("Shared header");
        loadedDocument.PlainText.ShouldContain("Second section");
    }

    private static Document CreateDocument(Section section) =>
        new(Guid.NewGuid(), "Headers", ImmutableArray.Create(section));

    private static MemoryStream SaveAndValidate(Document document)
    {
        var stream = new MemoryStream();
        DocxService.Save(document, stream);
        stream.Position = 0;

        using (var package = WordprocessingDocument.Open(stream, false))
        {
            var errors = new OpenXmlValidator()
                .Validate(package, TestContext.Current.CancellationToken)
                .ToList();
            errors.ShouldBeEmpty(string.Join(
                "\n",
                errors.Select(error => $"{error.Path?.XPath}: {error.Description}")));
        }

        stream.Position = 0;
        return stream;
    }
}
