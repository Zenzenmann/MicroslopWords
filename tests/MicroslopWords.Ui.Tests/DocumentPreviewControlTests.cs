using Avalonia;
using Avalonia.Headless.XUnit;
using MicroslopWords.App.Controls;
using MicroslopWords.Core.Documents;
using MicroslopWords.Layout;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class DocumentPreviewControlTests
{
    [AvaloniaFact]
    public void Measure_scales_the_page_with_zoom()
    {
        var control = new DocumentPreviewControl
        {
            Layout = CreateLayout("Hello world"),
            Zoom = 1,
        };

        control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        control.DesiredSize.Width.ShouldBe(864, 0.01);
        control.DesiredSize.Height.ShouldBe(1104, 0.01);

        control.Zoom = 2;
        control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        control.DesiredSize.Width.ShouldBe(1680, 0.01);
        control.DesiredSize.Height.ShouldBe(2160, 0.01);

        control.Zoom = 0.25;
        control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        control.DesiredSize.Width.ShouldBe(252, 0.01);
        control.DesiredSize.Height.ShouldBe(312, 0.01);
    }

    [AvaloniaFact]
    public void Zoom_is_coerced_to_the_supported_range()
    {
        var control = new DocumentPreviewControl();

        control.Zoom = 10;
        control.Zoom.ShouldBe(4);

        control.Zoom = 0.01;
        control.Zoom.ShouldBe(0.25);
    }

    [AvaloniaFact]
    public void TryHitTest_maps_content_coordinates_to_page_and_line_at_every_zoom()
    {
        var layout = CreateLayout("Hello world");
        var control = new DocumentPreviewControl { Layout = layout };

        foreach (var zoom in new[] { 0.25, 1.0, 4.0 })
        {
            control.Zoom = zoom;
            var scale = PageViewport.PageScale(zoom);
            var point = new Point(
                DocumentPreviewControl.OuterMarginDips + (72 * scale) + 1,
                DocumentPreviewControl.OuterMarginDips + (72 * scale) + 1);

            control.TryHitTest(point, out var page, out var line).ShouldBeTrue();
            page.ShouldNotBeNull();
            page.PageNumber.ShouldBe(1);
            line.ShouldNotBeNull();
            line.Text.ShouldBe("Hello world");
        }
    }

    [AvaloniaFact]
    public void TryHitTest_rejects_points_outside_the_page()
    {
        var control = new DocumentPreviewControl { Layout = CreateLayout("Hello world") };

        control.TryHitTest(new Point(1, 1), out var page, out var line).ShouldBeFalse();
        page.ShouldBeNull();
        line.ShouldBeNull();
    }

    private static DocumentLayout CreateLayout(string text) =>
        new LayoutEngine().Layout(Document.CreateEmpty() with
        {
            Sections =
            [
                Section.CreateEmpty() with
                {
                    Blocks = [ParagraphBlock.Create(text)],
                },
            ],
        });
}
