using System.Collections.Immutable;
using DocumentFormat.OpenXml.Packaging;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using MicroslopWords.Formats.Docx;
using Shouldly;

namespace MicroslopWords.Docx.Tests;

public sealed class DocxImageTests
{
    [Fact]
    public void SaveAndLoad_round_trips_an_inline_image()
    {
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(
                Run.Create("Before "),
                Run.CreateImage(CreatePngImage(altText: "A red rectangle")),
                Run.Create(" after")));
        var document = CreateDocument(paragraph);

        using var stream = new MemoryStream();
        DocxService.Save(document, stream);

        using (var package = WordprocessingDocument.Open(stream, false))
        {
            package.MainDocumentPart.ShouldNotBeNull();
            package.MainDocumentPart!.ImageParts.Count().ShouldBe(1);
        }

        stream.Position = 0;
        var loaded = DocxService.Load(stream, "Images", TestContext.Current.CancellationToken);

        loaded.IsSuccess.ShouldBeTrue();
        loaded.Diagnostics.ShouldBeEmpty();

        var loadedParagraph = loaded.Value!.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .Single();
        loadedParagraph.PlainText.ShouldBe("Before \uFFFC after");
        loadedParagraph.Runs.Length.ShouldBe(3);

        var image = loadedParagraph.Runs[1].EffectiveImage;
        image.ShouldNotBeNull();
        image!.Format.ShouldBe(ImageFormat.Png);
        image.Data.ToArray().ShouldBe(ImageTestData.Png());
        image.PixelWidth.ShouldBe(100);
        image.PixelHeight.ShouldBe(50);
        image.WidthPoints.ShouldBe(75, 0.01);
        image.HeightPoints.ShouldBe(37.5, 0.01);
        image.AltText.ShouldBe("A red rectangle");
        image.IsPlaceholder.ShouldBeFalse();
    }

    [Fact]
    public void Save_deduplicates_identical_image_media_and_keeps_both_placements()
    {
        var data = ImageTestData.Png();
        var first = Run.CreateImage(CreatePngImage(data, "First"));
        var second = Run.CreateImage(CreatePngImage(data, "Second"));
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(first, Run.Create(" "), second));
        var document = CreateDocument(paragraph);

        using var stream = new MemoryStream();
        DocxService.Save(document, stream);

        using (var package = WordprocessingDocument.Open(stream, false))
        {
            package.MainDocumentPart!.ImageParts.Count().ShouldBe(1);
        }

        stream.Position = 0;
        var loaded = DocxService.Load(stream, "Images", TestContext.Current.CancellationToken);
        var loadedParagraph = loaded.Value!.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .Single();

        loadedParagraph.Runs.Count(run => run.EffectiveImage is not null).ShouldBe(2);
        loadedParagraph.Runs[0].EffectiveImage!.AltText.ShouldBe("First");
        loadedParagraph.Runs[2].EffectiveImage!.AltText.ShouldBe("Second");
    }

    [Fact]
    public void Load_replaces_a_missing_image_part_with_a_placeholder_and_warning()
    {
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.CreateImage(CreatePngImage())));
        var document = CreateDocument(paragraph);

        using var stream = new MemoryStream();
        DocxService.Save(document, stream);

        stream.Position = 0;
        using (var package = WordprocessingDocument.Open(stream, true))
        {
            var imagePart = package.MainDocumentPart!.ImageParts.Single();
            package.MainDocumentPart.DeletePart(imagePart);
        }

        stream.Position = 0;
        var loaded = DocxService.Load(stream, "Missing", TestContext.Current.CancellationToken);

        loaded.IsSuccess.ShouldBeTrue();
        loaded.Diagnostics.ShouldContain(diagnostic =>
            diagnostic.Code == DocxDiagnosticCodes.UnsupportedImage
            && diagnostic.Severity == DiagnosticSeverity.Warning);

        var image = loaded.Value!.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .Single()
            .Runs
            .Single()
            .EffectiveImage;

        image.ShouldNotBeNull();
        image!.IsPlaceholder.ShouldBeTrue();
        image.Data.ShouldBeEmpty();
    }

    [Fact]
    public void SaveAndLoad_round_trips_gif_bytes_without_reencoding()
    {
        var gif = new InlineImage(
            ImageFormat.Gif,
            "blue.gif",
            ImmutableArray.Create(ImageTestData.Gif()),
            32,
            16,
            24,
            12,
            "A blue square");
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.CreateImage(gif)));

        using var stream = new MemoryStream();
        DocxService.Save(CreateDocument(paragraph), stream);
        stream.Position = 0;

        var loaded = DocxService.Load(stream, "GIF", TestContext.Current.CancellationToken);
        var image = loaded.Value!.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .Single()
            .Runs
            .Single()
            .EffectiveImage;

        image.ShouldNotBeNull();
        image!.Format.ShouldBe(ImageFormat.Gif);
        image.Data.ToArray().ShouldBe(ImageTestData.Gif());
        image.PixelWidth.ShouldBe(32);
        image.PixelHeight.ShouldBe(16);
    }

    private static InlineImage CreatePngImage(
        byte[]? data = null,
        string altText = "") =>
        new(
            ImageFormat.Png,
            "red.png",
            ImmutableArray.Create(data ?? ImageTestData.Png()),
            100,
            50,
            75,
            37.5,
            altText);

    private static Document CreateDocument(params ParagraphBlock[] paragraphs)
    {
        var section = new Section(
            Guid.NewGuid(),
            paragraphs.Cast<Block>().ToImmutableArray());

        return new Document(Guid.NewGuid(), "Test", ImmutableArray.Create(section));
    }
}
