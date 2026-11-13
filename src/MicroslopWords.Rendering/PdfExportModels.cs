using System.Collections.Immutable;
using MicroslopWords.Core.Diagnostics;

namespace MicroslopWords.Rendering;

public static class RenderingDiagnosticCodes
{
    public const string InvalidPageRange = "MSW6001";
    public const string PdfExportFailed = "MSW6002";
    public const string PrintFailed = "MSW6003";
    public const string PrintersUnavailable = "MSW6004";
}

public enum PageRangeKind
{
    All,
    Current,
    Custom,
}

public sealed record PageRange(
    PageRangeKind Kind,
    int CurrentPage = 1,
    int FromPage = 1,
    int ToPage = 1)
{
    public OperationResult<ImmutableArray<int>> Resolve(int pageCount)
    {
        if (pageCount <= 0)
        {
            return OperationResult.Failure<ImmutableArray<int>>(
                RenderingDiagnosticCodes.InvalidPageRange,
                "The document has no pages to export.");
        }

        switch (Kind)
        {
            case PageRangeKind.All:
                return OperationResult.Success(
                    Enumerable.Range(1, pageCount).ToImmutableArray());

            case PageRangeKind.Current:
                return CurrentPage < 1 || CurrentPage > pageCount
                    ? Invalid()
                    : OperationResult.Success(ImmutableArray.Create(CurrentPage));

            case PageRangeKind.Custom:
                return FromPage < 1 || ToPage > pageCount || FromPage > ToPage
                    ? Invalid()
                    : OperationResult.Success(
                        Enumerable.Range(FromPage, ToPage - FromPage + 1)
                            .ToImmutableArray());

            default:
                return Invalid();
        }
    }

    private static OperationResult<ImmutableArray<int>> Invalid() =>
        OperationResult.Failure<ImmutableArray<int>>(
            RenderingDiagnosticCodes.InvalidPageRange,
            "Choose a page range inside the document.");
}

public sealed record PdfExportOptions(
    PageRange Range,
    bool IncludeHeadersFooters = true,
    string? Title = null,
    DateTimeOffset? Timestamp = null);

public sealed record PdfExportResult(
    ImmutableArray<int> Pages,
    int PageCount);

public sealed record PrintSummary(
    int PageCount,
    int Copies,
    string? PrinterName);

public sealed record PrintRequest(
    string Title,
    byte[] Pdf,
    ImmutableArray<int> Pages,
    int Copies,
    string? PrinterName);
