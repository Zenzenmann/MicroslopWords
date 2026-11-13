using MicroslopWords.Rendering;

namespace MicroslopWords.App.Services;

public enum PrintExportMode
{
    Print,
    ExportPdf,
}

public sealed record PrintExportRequest(
    PrintExportMode Mode,
    string DocumentTitle,
    int PageCount,
    int CurrentPage,
    IReadOnlyList<string> Printers);

public sealed record PrintExportOptions(
    PageRange Range,
    bool IncludeHeadersFooters,
    int Copies,
    string? PrinterName);

public interface IPrintExportDialog
{
    Task<PrintExportOptions?> ShowAsync(
        PrintExportRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailablePrintExportDialog : IPrintExportDialog
{
    public Task<PrintExportOptions?> ShowAsync(
        PrintExportRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<PrintExportOptions?>(null);
}
