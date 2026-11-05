using MicroslopWords.App.Controls;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class PageViewportTests
{
    [Fact]
    public void ClampZoom_limits_zoom_to_supported_range()
    {
        PageViewport.ClampZoom(0.01).ShouldBe(0.25);
        PageViewport.ClampZoom(0.25).ShouldBe(0.25);
        PageViewport.ClampZoom(4).ShouldBe(4);
        PageViewport.ClampZoom(10).ShouldBe(4);
    }

    [Fact]
    public void ZoomIn_and_ZoomOut_clamp_at_the_limits()
    {
        PageViewport.ZoomIn(4).ShouldBe(4);
        PageViewport.ZoomOut(0.25).ShouldBe(0.25);
        PageViewport.ZoomIn(1).ShouldBe(1.1, 0.0001);
        PageViewport.ZoomOut(1).ShouldBe(0.9, 0.0001);
    }

    [Fact]
    public void ZoomForFitWidth_computes_zoom_from_viewport_and_page_width()
    {
        var zoom = PageViewport.ZoomForFitWidth(864, 612, 24);

        zoom.ShouldBe(1, 0.0001);
    }

    [Fact]
    public void ZoomForFitPage_uses_the_tighter_of_width_and_height()
    {
        var zoom = PageViewport.ZoomForFitPage(864, 1104, 612, 792, 24, 24);

        zoom.ShouldBe(1, 0.0001);
    }

    [Fact]
    public void OffsetForAnchoredZoom_keeps_pointer_over_same_document_point()
    {
        var (offsetX, offsetY) = PageViewport.OffsetForAnchoredZoom(
            oldZoom: 1,
            newZoom: 2,
            anchorX: 400,
            anchorY: 300,
            scrollX: 200,
            scrollY: 100);

        offsetX.ShouldBe(600, 0.0001);
        offsetY.ShouldBe(400, 0.0001);

        var oldLayout = PageViewport.ToLayoutPoint(400, 300, 1);
        var newLayout = PageViewport.ToLayoutPoint(offsetX + 200, offsetY + 200, 2);

        newLayout.X.ShouldBe(oldLayout.X, 0.0001);
        newLayout.Y.ShouldBe(oldLayout.Y, 0.0001);
    }

    [Fact]
    public void ToLayoutPoint_converts_content_coordinates_to_points()
    {
        var (x, y) = PageViewport.ToLayoutPoint(96, 144, 1);

        x.ShouldBe(72, 0.0001);
        y.ShouldBe(108, 0.0001);
    }
}
