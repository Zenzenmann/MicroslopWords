using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using Shouldly;

namespace MicroslopWords.Layout.Tests;

public sealed class LayoutImageTests
{
    [Fact]
    public void Layout_places_inline_image_at_its_model_size()
    {
        var image = CreateImage(widthPoints: 75, heightPoints: 37.5);
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(
                Run.Create("A"),
                Run.CreateImage(image),
                Run.Create("B")));

        var layout = new LayoutEngine().Layout(CreateDocument(paragraph));
        var line = layout.Pages[0].Paragraphs.Single().Lines.Single();

        line.Text.ShouldBe("A\uFFFCB");
        line.Images.Length.ShouldBe(1);
        line.Images[0].Image.WidthPoints.ShouldBe(75);
        line.Images[0].Bounds.Width.ShouldBe(75);
        line.Images[0].Bounds.Height.ShouldBe(37.5, 0.0001);
        line.Bounds.Height.ShouldBe(37.5, 0.0001);
        line.Images[0].Bounds.Bottom.ShouldBe(line.Bounds.Bottom, 0.0001);
        line.Spans.Length.ShouldBe(2);
        line.Spans[0].Width.ShouldBeGreaterThan(0);
        line.Spans[1].X.ShouldBeGreaterThanOrEqualTo(75);
    }

    [Fact]
    public void Layout_caps_image_width_to_content_width_and_preserves_aspect_ratio()
    {
        var image = CreateImage(
            pixelWidth: 600,
            pixelHeight: 300,
            widthPoints: 600,
            heightPoints: 300);
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.CreateImage(image)));

        var layout = new LayoutEngine().Layout(CreateDocument(paragraph));
        var placement = layout.Pages[0].Paragraphs.Single().Lines.Single().Images.Single();

        placement.Bounds.Width.ShouldBe(468, 0.0001);
        placement.Bounds.Height.ShouldBe(234, 0.0001);
        placement.Bounds.Right.ShouldBeLessThanOrEqualTo(72 + 468 + 0.0001);
    }

    [Fact]
    public void Layout_caps_image_height_to_content_height()
    {
        var image = CreateImage(
            pixelWidth: 400,
            pixelHeight: 800,
            widthPoints: 400,
            heightPoints: 800);
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.CreateImage(image)));

        var layout = new LayoutEngine().Layout(CreateDocument(paragraph));
        var placement = layout.Pages[0].Paragraphs.Single().Lines.Single().Images.Single();

        placement.Bounds.Height.ShouldBe(648, 0.0001);
        placement.Bounds.Width.ShouldBe(324, 0.0001);
        layout.Pages.Length.ShouldBe(1);
    }

    [Fact]
    public void Layout_moves_image_to_the_next_page_when_it_does_not_fit()
    {
        var first = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.CreateImage(
                CreateImage(widthPoints: 300, heightPoints: 600))));
        var second = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.CreateImage(
                CreateImage(widthPoints: 300, heightPoints: 600))));

        var layout = new LayoutEngine().Layout(CreateDocument(first, second));

        layout.Pages.Length.ShouldBe(2);
        layout.Pages[0].Paragraphs.Single().Lines.Single().Images.Length.ShouldBe(1);
        layout.Pages[1].Paragraphs.Single().Lines.Single().Images.Length.ShouldBe(1);
    }

    [Fact]
    public void Layout_image_only_line_has_no_text_span_for_the_object_character()
    {
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.CreateImage(CreateImage())));

        var layout = new LayoutEngine().Layout(CreateDocument(paragraph));
        var line = layout.Pages[0].Paragraphs.Single().Lines.Single();

        line.Spans.ShouldBeEmpty();
        line.Images.Length.ShouldBe(1);
        line.Text.ShouldBe("\uFFFC");
    }

    private static InlineImage CreateImage(
        int pixelWidth = 100,
        int pixelHeight = 50,
        double widthPoints = 75,
        double heightPoints = 37.5) =>
        new(
            ImageFormat.Png,
            "image.png",
            ImmutableArray.Create<byte>(1, 2, 3),
            pixelWidth,
            pixelHeight,
            widthPoints,
            heightPoints,
            "Alt text");

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
