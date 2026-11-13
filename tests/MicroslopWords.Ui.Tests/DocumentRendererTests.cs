using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using MicroslopWords.Layout;
using MicroslopWords.Rendering;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class DocumentRendererTests
{
    [Fact]
    public void RenderPagePng_returns_png_for_layout_page()
    {
        var document = new Document(
            Guid.NewGuid(),
            "Render",
            ImmutableArray.Create(new Section(
                Guid.NewGuid(),
                ImmutableArray.Create<Block>(ParagraphBlock.Create("Hello world")))));
        var layout = new LayoutEngine().Layout(document);

        var png = new DocumentRenderer().RenderPagePng(layout.Pages[0]);

        png.ShouldNotBeEmpty();
        png[0].ShouldBe((byte)0x89);
        png[1].ShouldBe((byte)0x50);
        png[2].ShouldBe((byte)0x4E);
        png[3].ShouldBe((byte)0x47);
    }

    [Fact]
    public void RenderPage_draws_inline_image_pixels()
    {
        var image = new InlineImage(
            ImageFormat.Png,
            "red.png",
            ImmutableArray.Create(Convert.FromBase64String(PngBase64)),
            100,
            50,
            75,
            37.5,
            "Red");
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.CreateImage(image)));
        var document = CreateDocument(paragraph);
        var layout = new LayoutEngine().Layout(document);
        var placement = layout.Pages[0].Paragraphs.Single().Lines.Single().Images.Single();

        using var bitmap = new DocumentRenderer().RenderPage(layout.Pages[0]);
        var pixel = bitmap.GetPixel(
            (int)(placement.Bounds.X + (placement.Bounds.Width / 2)),
            (int)(placement.Bounds.Y + (placement.Bounds.Height / 2)));

        ((int)pixel.Red).ShouldBeGreaterThan(150);
        ((int)pixel.Green).ShouldBeLessThan(100);
    }

    [Fact]
    public void RenderPage_draws_a_placeholder_for_missing_image_data()
    {
        var image = new InlineImage(
            ImageFormat.Png,
            "missing.png",
            ImmutableArray<byte>.Empty,
            0,
            0,
            75,
            37.5,
            "Missing",
            IsAvailable: false);
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.CreateImage(image)));
        var layout = new LayoutEngine().Layout(CreateDocument(paragraph));

        using var bitmap = new DocumentRenderer().RenderPage(layout.Pages[0]);

        bitmap.ShouldNotBeNull();
    }

    private static Document CreateDocument(ParagraphBlock paragraph) =>
        new(
            Guid.NewGuid(),
            "Render",
            ImmutableArray.Create(new Section(
                Guid.NewGuid(),
                ImmutableArray.Create<Block>(paragraph))));

    private const string PngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAGQAAAAyCAIAAAAlV+npAAAAhUlEQVR4nO3SMREAIBADQUAI/qUgCwt//W6d6ib73buYOcMdYjWeFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYi15j5uMQFoB1/W+QAAAABJRU5ErkJggg==";
}
