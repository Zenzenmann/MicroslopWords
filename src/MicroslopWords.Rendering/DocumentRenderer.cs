using MicroslopWords.Layout;
using SkiaSharp;

namespace MicroslopWords.Rendering;

public sealed class DocumentRenderer
{
    private readonly SKColor _lineColor;

    public DocumentRenderer(SKColor? lineColor = null)
    {
        _lineColor = lineColor ?? new SKColor(210, 210, 210);
    }

    public IReadOnlyList<SKBitmap> RenderDocument(DocumentLayout layout, float scale = 1f)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var bitmaps = new List<SKBitmap>(layout.Pages.Length);

        foreach (var page in layout.Pages)
        {
            bitmaps.Add(RenderPage(page, scale));
        }

        return bitmaps;
    }

    public SKBitmap RenderPage(PageLayout page, float scale = 1f)
    {
        ArgumentNullException.ThrowIfNull(page);

        var width = Math.Max(1, (int)Math.Ceiling(page.Bounds.Width * scale));
        var height = Math.Max(1, (int)Math.Ceiling(page.Bounds.Height * scale));
        var bitmap = new SKBitmap(width, height);

        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var linePaint = new SKPaint
        {
            Color = _lineColor,
            StrokeWidth = 1,
            IsAntialias = true,
        };

        foreach (var line in page.Paragraphs.SelectMany(paragraph => paragraph.Lines))
        {
            var rect = SKRect.Create(
                (float)(line.Bounds.X * scale),
                (float)(line.Bounds.Y * scale),
                (float)(Math.Max(1, line.Bounds.Width) * scale),
                (float)(Math.Max(1, line.Bounds.Height) * scale));

            canvas.DrawRect(rect, linePaint);
        }

        canvas.Flush();
        return bitmap;
    }
}
