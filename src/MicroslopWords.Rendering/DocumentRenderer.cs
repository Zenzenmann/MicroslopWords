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
            var text = line.Text.Replace(
                Core.Documents.InlineImage.PlaceholderCharacter,
                ' ');

            canvas.DrawText(text, x, baseline, font, textPaint);

            foreach (var image in line.Images)
            {
                DrawImage(canvas, image, scale, textPaint);
            }
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

    private void DrawImage(
        SKCanvas canvas,
        InlineImageLayout placement,
        float scale,
        SKPaint textPaint)
    {
        var left = (float)(placement.Bounds.X * scale);
        var top = (float)(placement.Bounds.Y * scale);
        var width = Math.Max(1, (float)(placement.Bounds.Width * scale));
        var height = Math.Max(1, (float)(placement.Bounds.Height * scale));
        var destination = new SKRect(left, top, left + width, top + height);

        if (!placement.Image.IsPlaceholder)
        {
            using var bitmap = SKBitmap.Decode(placement.Image.Data.ToArray());

            if (bitmap is not null)
            {
                using var image = SKImage.FromBitmap(bitmap);
                canvas.DrawImage(image, destination);
                return;
            }
        }

        using var fill = new SKPaint
        {
            Color = new SKColor(0xF2, 0xF2, 0xF2),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        using var border = new SKPaint
        {
            Color = new SKColor(0x9A, 0x9A, 0x9A),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(1, scale),
            IsAntialias = true,
        };

        canvas.DrawRect(destination, fill);
        canvas.DrawRect(destination, border);

        var label = placement.Image.IsPlaceholder
            ? "Image unavailable"
            : placement.Image.AccessibleName;

        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        using var font = new SKFont(_typeface, Math.Max(8, (float)(10 * scale)));
        var textWidth = font.MeasureText(label, textPaint);
        var textX = left + Math.Max(2, ((width - textWidth) / 2));
        var textY = top + (height / 2) + (font.Size / 3);

        canvas.DrawText(label, textX, textY, font, textPaint);
    }
}
