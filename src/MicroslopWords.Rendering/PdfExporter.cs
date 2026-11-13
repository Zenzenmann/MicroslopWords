using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Layout;
using SkiaSharp;

namespace MicroslopWords.Rendering;

public static class PdfExporter
{
    // A fixed default keeps repeated exports byte-for-byte reproducible.
    private static readonly DateTimeOffset DefaultTimestamp =
        new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Writes the selected layout pages to a PDF. Callers that need
    /// all-or-nothing output must write through a temporary file and replace
    /// the destination after this method succeeds; cancellation or failure can
    /// leave a partial stream behind.
    /// </summary>
    public static OperationResult<PdfExportResult> Export(
        DocumentLayout layout,
        Stream output,
        PdfExportOptions options,
        FontCatalog? fontCatalog = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(options);

        var pages = options.Range.Resolve(layout.Pages.Length);

        if (!pages.IsSuccess || pages.Value.IsDefault)
        {
            return OperationResult.Failure<PdfExportResult>(
                pages.Diagnostics.ToArray());
        }

        var selected = pages.Value;
        var pagesByNumber = layout.Pages.ToDictionary(page => page.PageNumber);
        var timestamp = options.Timestamp ?? DefaultTimestamp;

        try
        {
            var metadata = new SKDocumentPdfMetadata
            {
                Title = options.Title ?? string.Empty,
                Creator = "MicroslopWords",
                Producer = "MicroslopWords",
                Creation = timestamp.UtcDateTime,
                Modified = timestamp.UtcDateTime,
                EncodingQuality = 101,
                RasterDpi = 72,
            };
            using var document = SKDocument.CreatePdf(output, metadata);

            if (document is null)
            {
                return Failure("The PDF writer could not be opened.");
            }

            using var fonts = new SkiaFontResolver(fontCatalog);
            using var renderer = new PageRenderer(fonts);

            foreach (var pageNumber in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!pagesByNumber.TryGetValue(pageNumber, out var page))
                {
                    return Failure($"Page {pageNumber} is not present in the layout.");
                }

                using var canvas = document.BeginPage(
                    (float)page.Bounds.Width,
                    (float)page.Bounds.Height);
                canvas.Clear(SKColors.White);
                renderer.DrawPage(
                    canvas,
                    page,
                    options.IncludeHeadersFooters,
                    scale: 1f);
                document.EndPage();
            }

            document.Close();

            return OperationResult.Success(
                new PdfExportResult(selected, layout.Pages.Length));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or IOException)
        {
            return Failure(exception.Message);
        }
    }

    private static OperationResult<PdfExportResult> Failure(string message) =>
        OperationResult.Failure<PdfExportResult>(
            RenderingDiagnosticCodes.PdfExportFailed,
            string.IsNullOrWhiteSpace(message)
                ? "The PDF could not be produced."
                : message);
}
