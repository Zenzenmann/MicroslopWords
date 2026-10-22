using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MicroslopWords.Layout;
using MicroslopWords.Rendering;

namespace MicroslopWords.App.Controls;

public sealed class DocumentPreviewControl : Control
{
    private const double PageScale = 96.0 / 72.0;
    private const double PageGap = 24;
    private const double OuterMargin = 24;

    public static readonly StyledProperty<DocumentLayout?> LayoutProperty =
        AvaloniaProperty.Register<DocumentPreviewControl, DocumentLayout?>(nameof(Layout));

    private readonly DocumentRenderer _renderer = new();
    private readonly List<Bitmap> _pageBitmaps = [];
    private readonly Pen _pageBorder = new(new SolidColorBrush(Color.Parse("#C9C9C9")), 1);
    private DocumentLayout? _renderedLayout;

    public DocumentLayout? Layout
    {
        get => GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LayoutProperty)
        {
            ClearPageBitmaps();
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Layout is null)
        {
            return default;
        }

        var width = (Layout.PageWidthPoints * PageScale) + (OuterMargin * 2);
        var pageHeight = Layout.Pages.Sum(page => page.Bounds.Height * PageScale);
        var gapHeight = PageGap * Math.Max(0, Layout.Pages.Length - 1);
        var height = pageHeight + gapHeight + (OuterMargin * 2);

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

        if (!ReferenceEquals(_renderedLayout, layout))
        {
            RebuildPageBitmaps(layout);
        }

        var pageWidth = layout.PageWidthPoints * PageScale;
        var y = OuterMargin;

        for (var index = 0; index < _pageBitmaps.Count; index++)
        {
            var page = layout.Pages[index];
            var pageHeight = page.Bounds.Height * PageScale;
            var destination = new Rect(OuterMargin, y, pageWidth, pageHeight);

            context.DrawImage(_pageBitmaps[index], destination);
            context.DrawRectangle(null, _pageBorder, destination);

            y += pageHeight + PageGap;
        }
    }

    private void RebuildPageBitmaps(DocumentLayout layout)
    {
        ClearPageBitmaps();
        _renderedLayout = layout;

        foreach (var page in layout.Pages)
        {
            var png = _renderer.RenderPagePng(page, (float)PageScale);
            using var stream = new MemoryStream(png);
            _pageBitmaps.Add(new Bitmap(stream));
        }
    }

    private void ClearPageBitmaps()
    {
        foreach (var bitmap in _pageBitmaps)
        {
            bitmap.Dispose();
        }

        _pageBitmaps.Clear();
        _renderedLayout = null;
    }
}
