namespace MicroslopWords.App.Controls;

public enum ZoomFitMode
{
    Custom,
    FitWidth,
    FitPage,
}

public static class PageViewport
{
    public const double MinZoom = 0.25;
    public const double MaxZoom = 4.0;

    public const double PointsToDips = 96.0 / 72.0;

    public const double OuterMarginDips = 24;

    public const double PageGapDips = 32;

    public static double ClampZoom(double zoom) => Math.Clamp(zoom, MinZoom, MaxZoom);

    public static double PageScale(double zoom) => PointsToDips * ClampZoom(zoom);

    public static double ZoomIn(double zoom, double step = 0.1) =>
        ClampZoom(ClampZoom(zoom) + step);

    public static double ZoomOut(double zoom, double step = 0.1) =>
        ClampZoom(ClampZoom(zoom) - step);

    public static double ZoomForFitWidth(
        double viewportWidthDips,
        double pageWidthPoints,
        double horizontalMarginDips)
    {
        if (viewportWidthDips <= 0 || pageWidthPoints <= 0)
        {
            return 1;
        }

        var available = Math.Max(1, viewportWidthDips - (horizontalMarginDips * 2));
        return ClampZoom(available / (pageWidthPoints * PointsToDips));
    }

    public static double ZoomForFitPage(
        double viewportWidthDips,
        double viewportHeightDips,
        double pageWidthPoints,
        double pageHeightPoints,
        double horizontalMarginDips,
        double verticalMarginDips)
    {
        if (viewportWidthDips <= 0
            || viewportHeightDips <= 0
            || pageWidthPoints <= 0
            || pageHeightPoints <= 0)
        {
            return 1;
        }

        var availableWidth = Math.Max(1, viewportWidthDips - (horizontalMarginDips * 2));
        var availableHeight = Math.Max(1, viewportHeightDips - (verticalMarginDips * 2));

        return ClampZoom(Math.Min(
            availableWidth / (pageWidthPoints * PointsToDips),
            availableHeight / (pageHeightPoints * PointsToDips)));
    }

    public static (double X, double Y) OffsetForAnchoredZoom(
        double oldZoom,
        double newZoom,
        double anchorX,
        double anchorY,
        double scrollX,
        double scrollY)
    {
        var oldScale = PageScale(oldZoom);
        var newScale = PageScale(newZoom);

        var layoutX = anchorX / oldScale;
        var layoutY = anchorY / oldScale;

        return (
            Math.Max(0, (layoutX * newScale) - anchorX + scrollX),
            Math.Max(0, (layoutY * newScale) - anchorY + scrollY));
    }

    public static (double X, double Y) ToLayoutPoint(double contentX, double contentY, double zoom)
    {
        var scale = PageScale(zoom);
        return (contentX / scale, contentY / scale);
    }
}
