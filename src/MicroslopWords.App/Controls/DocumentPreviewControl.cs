using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using MicroslopWords.Layout;

namespace MicroslopWords.App.Controls;

public sealed class DocumentPreviewControl : Control
{
    public const double OuterMarginDips = PageViewport.OuterMarginDips;
    public const double PageGapDips = PageViewport.PageGapDips;

    public static readonly StyledProperty<DocumentLayout?> LayoutProperty =
        AvaloniaProperty.Register<DocumentPreviewControl, DocumentLayout?>(nameof(Layout));

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<DocumentPreviewControl, double>(
            nameof(Zoom),
            1.0,
            coerce: CoerceZoom);

    public static readonly StyledProperty<int> CurrentPageProperty =
        AvaloniaProperty.Register<DocumentPreviewControl, int>(nameof(CurrentPage), 1);

    private static readonly IBrush PageBrush =
        new SolidColorBrush(Color.Parse("#FFFEFB"));

    private static readonly IBrush PageNumberBrush =
        new SolidColorBrush(Color.Parse("#5F6A76"));

    private static readonly IBrush TextBrush =
        new SolidColorBrush(Color.Parse("#1B1F24"));

    private static readonly Pen PageBorder =
        new(new SolidColorBrush(Color.Parse("#C9C9C9")), 1);

    private ScrollViewer? _scrollViewer;
    private Vector? _pendingScrollOffset;
    private double? _pointerAnchorX;
    private double? _pointerAnchorY;

    public DocumentLayout? Layout
    {
        get => GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public int CurrentPage
    {
        get => GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _scrollViewer = this.FindAncestorOfType<ScrollViewer>();

        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged += OnScrollChanged;
            _scrollViewer.SizeChanged += OnScrollViewerSizeChanged;
        }

        LayoutUpdated += OnLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdated;

        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged -= OnScrollChanged;
            _scrollViewer.SizeChanged -= OnScrollViewerSizeChanged;
            _scrollViewer = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LayoutProperty)
        {
            InvalidateMeasure();
            InvalidateVisual();
            UpdateCurrentPage();
        }
        else if (change.Property == ZoomProperty)
        {
            var oldZoom = change.GetOldValue<double>();
            var newZoom = change.GetNewValue<double>();

            if (!AreClose(oldZoom, newZoom))
            {
                AdjustScrollForZoom(oldZoom, newZoom);
            }

            InvalidateMeasure();
            InvalidateVisual();
            UpdateCurrentPage();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Layout is null)
        {
            return default;
        }

        var scale = PageViewport.PageScale(Zoom);
        var widestPage = Layout.Pages.Length == 0
            ? Layout.PageWidthPoints
            : Layout.Pages.Max(page => page.Bounds.Width);
        var width = (widestPage * scale) + (OuterMarginDips * 2);
        var height = Layout.Pages.Sum(page => page.Bounds.Height * scale)
            + (PageGapDips * Math.Max(0, Layout.Pages.Length - 1))
            + (OuterMarginDips * 2);

        return new Size(width, height);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var layout = Layout;

        if (layout is null)
        {
            return;
        }

        var scale = PageViewport.PageScale(Zoom);
        var widestPage = (layout.Pages.Length == 0
                ? layout.PageWidthPoints
                : layout.Pages.Max(page => page.Bounds.Width)) * scale;
        var y = OuterMarginDips;

        foreach (var page in layout.Pages)
        {
            var pageWidth = page.Bounds.Width * scale;
            var pageHeight = page.Bounds.Height * scale;
            var pageRect = new Rect(
                OuterMarginDips + ((widestPage - pageWidth) / 2),
                y,
                pageWidth,
                pageHeight);

            context.FillRectangle(PageBrush, pageRect);

            using (context.PushClip(pageRect))
            {
                foreach (var line in page.Paragraphs.SelectMany(paragraph => paragraph.Lines))
                {
                    DrawLine(context, line, pageRect, scale);
                }
            }

            context.DrawRectangle(null, PageBorder, pageRect);
            DrawPageNumber(context, page.PageNumber, pageRect);

            y += pageHeight + PageGapDips;
        }
    }

    public bool TryHitTest(Point contentPoint, out PageLayout? page, out LineLayout? line)
    {
        page = null;
        line = null;

        var layout = Layout;

        if (layout is null)
        {
            return false;
        }

        var scale = PageViewport.PageScale(Zoom);
        var widestPage = (layout.Pages.Length == 0
                ? layout.PageWidthPoints
                : layout.Pages.Max(page => page.Bounds.Width)) * scale;
        var y = OuterMarginDips;

        foreach (var candidate in layout.Pages)
        {
            var pageWidth = candidate.Bounds.Width * scale;
            var pageRect = new Rect(
                OuterMarginDips + ((widestPage - pageWidth) / 2),
                y,
                pageWidth,
                candidate.Bounds.Height * scale);

            if (pageRect.Contains(contentPoint))
            {
                page = candidate;
                var layoutPoint = new LayoutPoint(
                    (contentPoint.X - pageRect.X) / scale,
                    (contentPoint.Y - pageRect.Y) / scale);

                line = candidate.Paragraphs
                    .SelectMany(paragraph => paragraph.Lines)
                    .FirstOrDefault(candidateLine => Contains(candidateLine.Bounds, layoutPoint));

                return true;
            }

            y += pageRect.Height + PageGapDips;
        }

        return false;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) == 0)
        {
            base.OnPointerWheelChanged(e);
            return;
        }

        var pointer = e.GetPosition(this);
        var factor = e.Delta.Y >= 0 ? 1.1 : 1 / 1.1;
        var newZoom = PageViewport.ClampZoom(Zoom * factor);

        if (!AreClose(newZoom, Zoom))
        {
            _pointerAnchorX = pointer.X;
            _pointerAnchorY = pointer.Y;
            Zoom = newZoom;
        }

        e.Handled = true;
    }

    private static double CoerceZoom(AvaloniaObject sender, double value) =>
        PageViewport.ClampZoom(value);

    private void AdjustScrollForZoom(double oldZoom, double newZoom)
    {
        if (_scrollViewer is null)
        {
            return;
        }

        var anchorX = _pointerAnchorX
            ?? (_scrollViewer.Offset.X + (_scrollViewer.Viewport.Width / 2));
        var anchorY = _pointerAnchorY
            ?? (_scrollViewer.Offset.Y + (_scrollViewer.Viewport.Height / 2));

        var (offsetX, offsetY) = PageViewport.OffsetForAnchoredZoom(
            oldZoom,
            newZoom,
            anchorX,
            anchorY,
            _scrollViewer.Offset.X,
            _scrollViewer.Offset.Y);

        _pointerAnchorX = null;
        _pointerAnchorY = null;
        _pendingScrollOffset = new Vector(offsetX, offsetY);
        InvalidateMeasure();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_pendingScrollOffset is not { } offset || _scrollViewer is null)
        {
            return;
        }

        _pendingScrollOffset = null;
        _scrollViewer.Offset = offset;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) =>
        UpdateCurrentPage();

    private void OnScrollViewerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateCurrentPage();
        InvalidateMeasure();
    }

    private void UpdateCurrentPage()
    {
        if (_scrollViewer is null || Layout is null || Layout.Pages.Length == 0)
        {
            return;
        }

        var scale = PageViewport.PageScale(Zoom);
        var viewportCenter = _scrollViewer.Offset.Y + (_scrollViewer.Viewport.Height / 2);
        var y = OuterMarginDips;
        var currentPage = Layout.Pages[^1].PageNumber;

        foreach (var page in Layout.Pages)
        {
            var pageHeight = page.Bounds.Height * scale;

            if (viewportCenter < y + pageHeight)
            {
                currentPage = page.PageNumber;
                break;
            }

            y += pageHeight + PageGapDips;
        }

        CurrentPage = currentPage;
    }

    private static void DrawLine(
        DrawingContext context,
        LineLayout line,
        Rect pageRect,
        double scale)
    {
        if (line.Text.Length == 0)
        {
            return;
        }

        var typeface = new Typeface(
            new FontFamily(line.ResolvedFont.Family),
            line.ResolvedFont.Slant == FontSlant.Italic ? FontStyle.Italic : FontStyle.Normal,
            line.ResolvedFont.Weight == MicroslopWords.Layout.FontWeight.Bold
                ? Avalonia.Media.FontWeight.Bold
                : Avalonia.Media.FontWeight.Normal);

        var formatted = new FormattedText(
            line.Text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            Math.Max(1, line.FontSizePoints * scale),
            TextBrush);

        context.DrawText(
            formatted,
            new Point(
                pageRect.X + (line.Bounds.X * scale),
                pageRect.Y + (line.Bounds.Y * scale)));
    }

    private static void DrawPageNumber(DrawingContext context, int pageNumber, Rect pageRect)
    {
        var formatted = new FormattedText(
            $"Page {pageNumber}",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            11,
            PageNumberBrush);

        context.DrawText(formatted, new Point(pageRect.X, pageRect.Y - 18));
    }

    private static bool Contains(LayoutRect rect, LayoutPoint point) =>
        point.X >= rect.X
        && point.X <= rect.Right
        && point.Y >= rect.Y
        && point.Y <= rect.Bottom;

    private static bool AreClose(double left, double right) =>
        Math.Abs(left - right) < 0.0001;

    private readonly record struct LayoutPoint(double X, double Y);
}
