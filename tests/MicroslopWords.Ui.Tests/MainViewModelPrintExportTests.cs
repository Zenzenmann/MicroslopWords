using System.Collections.Immutable;
using MicroslopWords.App.Services;
using MicroslopWords.App.ViewModels;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using MicroslopWords.Formats.Docx;
using MicroslopWords.Layout;
using MicroslopWords.Rendering;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class MainViewModelPrintExportTests
{
    [Fact]
    public async Task ExportPdfCommand_writes_the_exported_bytes_atomically()
    {
        var picker = new FakePdfFilePicker("report.pdf");
        var writer = new FakeAtomicFileWriter();
        var dialog = new FakePrintExportDialog
        {
            Options = new PrintExportOptions(
                new PageRange(PageRangeKind.All),
                IncludeHeadersFooters: true,
                Copies: 1,
                PrinterName: null),
        };
        var exporter = new FakePdfExportService();
        using var viewModel = CreateViewModel(
            picker,
            writer,
            dialog,
            exporter,
            new FakePrintService());

        await viewModel.ExportPdfCommand.ExecuteAsync(null);

        writer.LastPath.ShouldBe("report.pdf");
        writer.LastBytes.ShouldBe([1, 2, 3]);
        viewModel.StatusText.ShouldBe("Exported 1 page(s) to report.pdf.");
        viewModel.IsExporting.ShouldBeFalse();
    }

    [Fact]
    public async Task ExportPdfCommand_rejects_an_invalid_range_before_writing()
    {
        var picker = new FakePdfFilePicker("report.pdf");
        var writer = new FakeAtomicFileWriter();
        var dialog = new FakePrintExportDialog
        {
            Options = new PrintExportOptions(
                new PageRange(
                    PageRangeKind.Custom,
                    FromPage: 2,
                    ToPage: 5),
                IncludeHeadersFooters: true,
                Copies: 1,
                PrinterName: null),
        };
        using var viewModel = CreateViewModel(
            picker,
            writer,
            dialog,
            new FakePdfExportService(),
            new FakePrintService());

        await viewModel.ExportPdfCommand.ExecuteAsync(null);

        writer.LastPath.ShouldBeNull();
        viewModel.ErrorMessage.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExportPdfCommand_reports_exporter_failure_without_writing()
    {
        var writer = new FakeAtomicFileWriter();
        var exporter = new FakePdfExportService
        {
            Result = OperationResult.Failure<PdfExportResult>(
                RenderingDiagnosticCodes.PdfExportFailed,
                "The PDF writer failed."),
        };
        using var viewModel = CreateViewModel(
            new FakePdfFilePicker("report.pdf"),
            writer,
            new FakePrintExportDialog
            {
                Options = new PrintExportOptions(
                    new PageRange(PageRangeKind.All),
                    true,
                    1,
                    null),
            },
            exporter,
            new FakePrintService());

        await viewModel.ExportPdfCommand.ExecuteAsync(null);

        writer.LastPath.ShouldBeNull();
        viewModel.ErrorMessage.ShouldBe("The PDF writer failed.");
        viewModel.IsExporting.ShouldBeFalse();
    }

    [Fact]
    public async Task PrintCommand_sends_selected_pages_copies_and_printer()
    {
        var exporter = new FakePdfExportService();
        var printer = new FakePrintService
        {
            PrintResult = OperationResult.Success(new PrintSummary(2, 2, "Office")),
        };
        var dialog = new FakePrintExportDialog
        {
            Options = new PrintExportOptions(
                new PageRange(PageRangeKind.Current, CurrentPage: 1),
                IncludeHeadersFooters: true,
                Copies: 2,
                PrinterName: "Office"),
        };
        using var viewModel = CreateViewModel(
            new FakePdfFilePicker(null),
            new FakeAtomicFileWriter(),
            dialog,
            exporter,
            printer);

        await viewModel.PrintCommand.ExecuteAsync(null);

        printer.LastRequest.ShouldNotBeNull();
        printer.LastRequest!.Pages.ShouldBe([1]);
        printer.LastRequest.Copies.ShouldBe(2);
        printer.LastRequest.PrinterName.ShouldBe("Office");
        printer.LastRequest.Pdf.ShouldBe([1, 2, 3]);
        viewModel.StatusText.ShouldContain("2 page(s)");
        viewModel.StatusText.ShouldContain("Office");
        viewModel.IsExporting.ShouldBeFalse();
    }

    [Fact]
    public async Task PrintCommand_passes_available_printers_to_the_dialog_and_reports_failure()
    {
        var printer = new FakePrintService
        {
            Printers = ["Office", "Lab"],
            PrintResult = OperationResult.Failure<PrintSummary>(
                RenderingDiagnosticCodes.PrintFailed,
                "Printer offline."),
        };
        var dialog = new FakePrintExportDialog
        {
            Options = new PrintExportOptions(
                new PageRange(PageRangeKind.All),
                true,
                1,
                "Office"),
        };
        using var viewModel = CreateViewModel(
            new FakePdfFilePicker(null),
            new FakeAtomicFileWriter(),
            dialog,
            new FakePdfExportService(),
            printer);

        await viewModel.PrintCommand.ExecuteAsync(null);

        dialog.LastRequest.ShouldNotBeNull();
        dialog.LastRequest!.Printers.ShouldBe(["Office", "Lab"]);
        viewModel.ErrorMessage.ShouldBe("Printer offline.");
    }

    private static MainViewModel CreateViewModel(
        IPdfFilePicker picker,
        IAtomicFileWriter writer,
        IPrintExportDialog dialog,
        IPdfExportService exporter,
        IPrintService printer) =>
        new(
            new UnavailableDocxFilePicker(),
            new DocxDocumentLoader(),
            new LayoutEngine(),
            new DocxDocumentSaver(),
            new UnavailableSaveConfirmation(),
            null,
            null,
            TimeSpan.FromSeconds(60),
            TimeProvider.System,
            clipboardService: null,
            imageFilePicker: new UnavailableImageFilePicker(),
            imageInsertDialog: new UnavailableImageInsertDialog(),
            imageDecoder: new HeaderOnlyImageDecoder(),
            pdfFilePicker: picker,
            pdfExportService: exporter,
            printService: printer,
            printExportDialog: dialog,
            atomicFileWriter: writer);

    private sealed class FakePdfFilePicker(string? path) : IPdfFilePicker
    {
        public Task<string?> PickSaveFileAsync(
            string suggestedFileName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(path);
    }

    private sealed class FakeAtomicFileWriter : IAtomicFileWriter
    {
        public string? LastPath { get; private set; }

        public byte[]? LastBytes { get; private set; }

        public void Write(
            string targetPath,
            Action<Stream> write,
            CancellationToken cancellationToken = default)
        {
            LastPath = targetPath;
            using var stream = new MemoryStream();
            write(stream);
            LastBytes = stream.ToArray();
        }
    }

    private sealed class FakePrintExportDialog : IPrintExportDialog
    {
        public PrintExportOptions? Options { get; set; }

        public PrintExportRequest? LastRequest { get; private set; }

        public Task<PrintExportOptions?> ShowAsync(
            PrintExportRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(Options);
        }
    }

    private sealed class FakePdfExportService : IPdfExportService
    {
        public OperationResult<PdfExportResult> Result { get; set; } =
            OperationResult.Success(new PdfExportResult([1], 1));

        public OperationResult<PdfExportResult> Export(
            DocumentLayout layout,
            Stream output,
            PdfExportOptions options,
            FontCatalog? fontCatalog,
            CancellationToken cancellationToken = default)
        {
            if (Result.IsSuccess)
            {
                output.Write([1, 2, 3]);
            }

            return Result;
        }
    }

    private sealed class FakePrintService : IPrintService
    {
        public IReadOnlyList<string> Printers { get; set; } = [];

        public OperationResult<PrintSummary> PrintResult { get; set; } =
            OperationResult.Success(new PrintSummary(1, 1, null));

        public PrintRequest? LastRequest { get; private set; }

        public Task<IReadOnlyList<string>> GetPrintersAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Printers);

        public Task<OperationResult<PrintSummary>> PrintAsync(
            PrintRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(PrintResult);
        }
    }
}
