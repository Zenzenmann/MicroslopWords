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

        using var fillPaint = new SKPaint
        {
            IsAntialias = true,
        };
        using var borderPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
        };

        foreach (var table in page.Tables)
        {
            foreach (var row in table.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    var rect = new SKRect(
                        (float)(cell.Bounds.X * scale),
                        (float)(cell.Bounds.Y * scale),
                        (float)(cell.Bounds.Right * scale),
                        (float)(cell.Bounds.Bottom * scale));

                    if (cell.ShadingHex is not null
                        || (row.IsHeader && cell.ShadingHex is null))
                    {
                        fillPaint.Color = cell.ShadingHex is null
                            ? SKColor.Parse("#F2F2F2")
                            : SKColor.Parse(cell.ShadingHex);
                        canvas.DrawRect(rect, fillPaint);
                    }

                    if (cell.Borders is { } borders)
                    {
                        borderPaint.Color = SKColor.Parse(borders.ColorHex);
                        borderPaint.StrokeWidth = (float)Math.Max(0.5, borders.WidthPoints * scale);
                        canvas.DrawRect(rect, borderPaint);
                    }
                }
            }
        }

        foreach (var line in PageParagraphs(page).SelectMany(paragraph => paragraph.Lines))
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

    private static IEnumerable<ParagraphLayout> PageParagraphs(PageLayout page) =>
        page.Paragraphs
            .Concat(page.Tables
                .SelectMany(table => table.Rows)
                .SelectMany(row => row.Cells)
                .SelectMany(cell => cell.Paragraphs));

    public byte[] RenderPagePng(PageLayout page, float scale = 1f)
    {
        using var bitmap = RenderPage(page, scale);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
