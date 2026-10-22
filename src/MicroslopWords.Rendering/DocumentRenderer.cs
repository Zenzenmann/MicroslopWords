using MicroslopWords.Layout;
using SkiaSharp;

namespace MicroslopWords.Rendering;

public sealed class DocumentRenderer
{
    private readonly SKTypeface _typeface;

    public DocumentRenderer(SKTypeface? typeface = null)
    {
        _typeface = typeface ?? SKTypeface.FromFamilyName("Arial") ?? SKTypeface.Default;
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

        using var textPaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
        };

        foreach (var line in page.Paragraphs.SelectMany(paragraph => paragraph.Lines))
        {
            var fontSize = (float)(line.FontSizePoints * scale);
            using var font = new SKFont(_typeface, fontSize);
            var x = (float)(line.Bounds.X * scale);
            var baseline = (float)((line.Bounds.Y + line.FontSizePoints) * scale);

            canvas.DrawText(line.Text, x, baseline, font, textPaint);
        }

        canvas.Flush();
        return bitmap;
    }

    public byte[] RenderPagePng(PageLayout page, float scale = 1f)
    {
        using var bitmap = RenderPage(page, scale);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
