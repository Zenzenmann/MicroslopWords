using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Layout;
using MicroslopWords.Rendering;

namespace MicroslopWords.App.Services;

public interface IPdfExportService
{
    OperationResult<PdfExportResult> Export(
        DocumentLayout layout,
        Stream output,
        PdfExportOptions options,
        FontCatalog? fontCatalog,
        CancellationToken cancellationToken = default);
}

public sealed class PdfExportService : IPdfExportService
{
    public OperationResult<PdfExportResult> Export(
        DocumentLayout layout,
        Stream output,
        PdfExportOptions options,
        FontCatalog? fontCatalog,
        CancellationToken cancellationToken = default) =>
        PdfExporter.Export(
            layout,
            output,
            options,
            fontCatalog,
            cancellationToken);
}
