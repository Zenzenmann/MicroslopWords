using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using MicroslopWords.Layout;
using SkiaSharp;

namespace MicroslopWords.Rendering;

/// <summary>
/// Draws one layout page with the same boxes, spans, tables, images, and
/// headers/footers used by the editor. The caller owns the canvas and page
/// background; this painter only emits document content.
/// </summary>
internal sealed class PageRenderer : IDisposable
{
    private readonly SkiaFontResolver _fonts;
    private readonly Dictionary<InlineImage, SKBitmap?> _images = [];

    public PageRenderer(SkiaFontResolver fonts)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        _fonts = fonts;
    }

    public void DrawPage(
        SKCanvas canvas,
        PageLayout page,
        bool includeHeadersFooters,
        float scale)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(page);

        DrawTableBackgrounds(canvas, page, scale);

        if (includeHeadersFooters && page.Header is not null)
        {
            DrawParagraphs(canvas, page.Header.Paragraphs, scale);
        }

        DrawParagraphs(canvas, page.Paragraphs, scale);

        foreach (var table in page.Tables)
        {
            foreach (var row in table.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    DrawParagraphs(canvas, cell.Paragraphs, scale);
                }
            }
        }

        if (includeHeadersFooters && page.Footer is not null)
        {
            DrawParagraphs(canvas, page.Footer.Paragraphs, scale);
        }

        DrawTableBorders(canvas, page, scale);
    }

    public void Dispose()
    {
        foreach (var bitmap in _images.Values)
        {
            bitmap?.Dispose();
        }

        _images.Clear();
    }

    private void DrawParagraphs(
        SKCanvas canvas,
        ImmutableArray<ParagraphLayout> paragraphs,
        float scale)
    {
        foreach (var paragraph in paragraphs)
        {
            if (paragraph.ListLabel is not null)
            {
                DrawText(
                    canvas,
                    paragraph.ListLabel,
                    paragraph.ListLabelX,
                    paragraph.Lines.Length == 0
                        ? paragraph.Bounds.Y
                        : paragraph.Lines[0].Bounds.Y,
                    paragraph.Lines.Length == 0
                        ? RunProperties.Default
                        : paragraph.Lines[0].Spans.Length == 0
                            ? new RunProperties
                            {
                                FontFamily = paragraph.Lines[0].ResolvedFont.Family,
                                FontSizePoints = paragraph.Lines[0].FontSizePoints,
                            }
                            : new RunProperties
                            {
                                FontFamily = paragraph.Lines[0].Spans[0].Font.Family,
                                FontSizePoints = paragraph.Lines[0].Spans[0].FontSizePoints,
                                ColorHex = paragraph.Lines[0].Spans[0].ColorHex,
                            },
                    scale);
            }

            foreach (var line in paragraph.Lines)
            {
                DrawLine(canvas, line, scale);
            }
        }
    }

    private void DrawLine(SKCanvas canvas, LineLayout line, float scale)
    {
        if (line.Text.Length == 0)
        {
            foreach (var image in line.Images)
            {
                DrawImage(canvas, image, scale);
            }

            return;
        }

        if (line.Spans.Length == 0)
        {
            var text = VisibleText(line.Text);

            if (!string.IsNullOrWhiteSpace(text))
            {
                DrawTextRun(
                    canvas,
                    text,
                    line.Bounds.X,
                    line.Bounds.Y,
                    line.ResolvedFont,
                    line.FontSizePoints,
                    "#000000",
                    null,
                    underline: false,
                    strikethrough: false,
                    scale);
            }
        }
        else
        {
            foreach (var span in line.Spans)
            {
                if (span.TextLength <= 0
                    || span.TextStart < 0
                    || span.TextStart + span.TextLength > line.Text.Length)
                {
                    continue;
                }

                DrawTextRun(
                    canvas,
                    VisibleText(line.Text.Substring(span.TextStart, span.TextLength)),
                    line.Bounds.X + span.X,
                    line.Bounds.Y,
                    span.Font,
                    span.FontSizePoints,
                    span.ColorHex,
                    span.HighlightHex,
                    span.Underline,
                    span.Strikethrough,
                    scale,
                    line.Bounds.Height);
            }
        }

        foreach (var image in line.Images)
        {
            DrawImage(canvas, image, scale);
        }
    }

    private void DrawText(
        SKCanvas canvas,
        string text,
        double x,
        double y,
        RunProperties properties,
        float scale) =>
        DrawTextRun(
            canvas,
            VisibleText(text),
            x,
            y,
            new FontKey(
                properties.FontFamily,
                properties.Bold ? FontWeight.Bold : FontWeight.Regular,
                properties.Italic ? FontSlant.Italic : FontSlant.Upright),
            properties.FontSizePoints,
            properties.ColorHex,
            properties.HighlightHex,
            properties.Underline,
            properties.Strikethrough,
            scale);

    private void DrawTextRun(
        SKCanvas canvas,
        string text,
        double x,
        double y,
        FontKey fontKey,
        double fontSizePoints,
        string colorHex,
        string? highlightHex,
        bool underline,
        bool strikethrough,
        float scale,
        double lineHeightPoints = 0)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var size = (float)Math.Max(1, fontSizePoints * scale);
        var baseline = (float)((y + fontSizePoints) * scale);
        var left = (float)(x * scale);
        using var font = new SKFont(_fonts.Resolve(fontKey), size);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = ParseColor(colorHex, SKColors.Black),
        };

        if (!string.IsNullOrWhiteSpace(highlightHex))
        {
            using var highlight = new SKPaint
            {
                IsAntialias = true,
                Color = ParseColor(highlightHex, SKColors.Transparent),
            };
            var height = lineHeightPoints > 0
                ? lineHeightPoints
                : fontSizePoints;
            canvas.DrawRect(
                new SKRect(
                    left,
                    (float)(y * scale),
                    left + font.MeasureText(text, paint),
                    (float)((y + height) * scale)),
                highlight);
        }

        canvas.DrawText(text, left, baseline, font, paint);

        if (!underline && !strikethrough)
        {
            return;
        }

        using var decoration = new SKPaint
        {
            IsAntialias = true,
            Color = paint.Color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(0.5f, scale),
        };
        var width = font.MeasureText(text, paint);
        var heightPoints = lineHeightPoints > 0
            ? lineHeightPoints
            : fontSizePoints;

        if (underline)
        {
            var yLine = (float)((y + heightPoints) * scale);
            canvas.DrawLine(left, yLine, left + width, yLine, decoration);
        }

        if (strikethrough)
        {
            var yLine = (float)((y + (heightPoints / 2)) * scale);
            canvas.DrawLine(left, yLine, left + width, yLine, decoration);
        }
    }

    private void DrawImage(
        SKCanvas canvas,
        InlineImageLayout placement,
        float scale)
    {
        var left = (float)(placement.Bounds.X * scale);
        var top = (float)(placement.Bounds.Y * scale);
        var width = Math.Max(1, (float)(placement.Bounds.Width * scale));
        var height = Math.Max(1, (float)(placement.Bounds.Height * scale));
        var destination = new SKRect(left, top, left + width, top + height);
        var bitmap = GetImageBitmap(placement.Image);

        if (bitmap is not null && !placement.Image.IsPlaceholder)
        {
            using var image = SKImage.FromBitmap(bitmap);
            canvas.DrawImage(image, destination);
            return;
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

        using var labelFont = new SKFont(
            _fonts.Resolve(new FontKey("Arial", FontWeight.Regular, FontSlant.Upright)),
            Math.Max(8, (float)(10 * scale)));
        using var labelPaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
        };
        var textWidth = labelFont.MeasureText(label, labelPaint);
        canvas.DrawText(
            label,
            left + Math.Max(2, ((width - textWidth) / 2)),
            top + (height / 2) + (labelFont.Size / 3),
            labelFont,
            labelPaint);
    }

    private SKBitmap? GetImageBitmap(InlineImage image)
    {
        if (_images.TryGetValue(image, out var cached))
        {
            return cached;
        }

        SKBitmap? bitmap = null;

        if (!image.IsPlaceholder)
        {
            try
            {
                bitmap = SKBitmap.Decode(image.Data.ToArray());
            }
            catch (Exception exception) when (
                exception is ArgumentException or InvalidOperationException)
            {
                bitmap = null;
            }
        }

        _images[image] = bitmap;
        return bitmap;
    }

    private static void DrawTableBackgrounds(
        SKCanvas canvas,
        PageLayout page,
        float scale)
    {
        using var fill = new SKPaint { IsAntialias = true };

        foreach (var table in page.Tables)
        {
            foreach (var row in table.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    if (cell.ShadingHex is null && !row.IsHeader)
                    {
                        continue;
                    }

                    fill.Color = cell.ShadingHex is null
                        ? new SKColor(0xF2, 0xF2, 0xF2)
                        : ParseColor(cell.ShadingHex, new SKColor(0xF2, 0xF2, 0xF2));
                    canvas.DrawRect(
                        ToRect(cell.Bounds, scale),
                        fill);
                }
            }
        }
    }

    private static void DrawTableBorders(
        SKCanvas canvas,
        PageLayout page,
        float scale)
    {
        using var border = new SKPaint
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
                    if (cell.Borders is not { } borders)
                    {
                        continue;
                    }

                    border.Color = ParseColor(borders.ColorHex, SKColors.Black);
                    border.StrokeWidth = (float)Math.Max(0.5, borders.WidthPoints * scale);
                    canvas.DrawRect(ToRect(cell.Bounds, scale), border);
                }
            }
        }
    }

    private static SKRect ToRect(LayoutRect bounds, float scale) =>
        new(
            (float)(bounds.X * scale),
            (float)(bounds.Y * scale),
            (float)(bounds.Right * scale),
            (float)(bounds.Bottom * scale));

    private static string VisibleText(string text) =>
        text.Replace(InlineImage.PlaceholderCharacter, ' ');

    private static SKColor ParseColor(string? value, SKColor fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            return SKColor.Parse(value);
        }
        catch (Exception exception) when (
            exception is ArgumentException or FormatException)
        {
            return fallback;
        }
    }
}
