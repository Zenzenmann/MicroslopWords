using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Rendering;

namespace MicroslopWords.App.Services;

public interface IPrintService
{
    Task<IReadOnlyList<string>> GetPrintersAsync(
        CancellationToken cancellationToken = default);

    Task<OperationResult<PrintSummary>> PrintAsync(
        PrintRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailablePrintService : IPrintService
{
    public Task<IReadOnlyList<string>> GetPrintersAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public Task<OperationResult<PrintSummary>> PrintAsync(
        PrintRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(OperationResult.Failure<PrintSummary>(
            RenderingDiagnosticCodes.PrintersUnavailable,
            "Printing is not available in this session."));
}

public sealed class SystemPrintService : IPrintService
{
    private readonly IProcessRunner _processRunner;
    private readonly string _temporaryDirectory;

    public SystemPrintService(
        IProcessRunner? processRunner = null,
        string? temporaryDirectory = null)
    {
        _processRunner = processRunner ?? new ProcessRunner();
        _temporaryDirectory = temporaryDirectory ?? Path.GetTempPath();
    }

    public async Task<IReadOnlyList<string>> GetPrintersAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = OperatingSystem.IsWindows()
                ? await _processRunner.RunAsync(
                    "powershell.exe",
                    [
                        "-NoProfile",
                        "-NonInteractive",
                        "-Command",
                        "Get-Printer | Select-Object -ExpandProperty Name",
                    ],
                    cancellationToken)
                : await _processRunner.RunAsync(
                    "lpstat",
                    ["-a"],
                    cancellationToken);

            if (result.ExitCode != 0)
            {
                return [];
            }

            return result.StandardOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => OperatingSystem.IsWindows()
                    ? line
                    : line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0])
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception
                or InvalidOperationException
                or IOException)
        {
            return [];
        }
    }

    public async Task<OperationResult<PrintSummary>> PrintAsync(
        PrintRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Pdf.Length == 0)
        {
            return Failure("There is nothing to print.");
        }

        if (request.Copies is < 1 or > 99)
        {
            return Failure("Choose between 1 and 99 copies.");
        }

        var temporaryPath = Path.Combine(
            _temporaryDirectory,
            $"msw-print-{Guid.NewGuid():N}.pdf");

        try
        {
            await File.WriteAllBytesAsync(
                temporaryPath,
                request.Pdf,
                cancellationToken);

            if (OperatingSystem.IsWindows())
            {
                var script = string.IsNullOrWhiteSpace(request.PrinterName)
                    ? $"Start-Process -FilePath '{EscapePowerShell(temporaryPath)}' -Verb Print -Wait"
                    : $"Start-Process -FilePath '{EscapePowerShell(temporaryPath)}' "
                        + $"-Verb PrintTo -ArgumentList '{EscapePowerShell(request.PrinterName)}' -Wait";

                for (var copy = 0; copy < request.Copies; copy++)
                {
                    var result = await _processRunner.RunAsync(
                        "powershell.exe",
                        ["-NoProfile", "-NonInteractive", "-Command", script],
                        cancellationToken);

                    if (result.ExitCode != 0)
                    {
                        return Failure(ProcessError(result));
                    }
                }
            }
            else
            {
                var arguments = new List<string>
                {
                    "-n",
                    request.Copies.ToString(System.Globalization.CultureInfo.InvariantCulture),
                };

                if (!string.IsNullOrWhiteSpace(request.PrinterName))
                {
                    arguments.Add("-d");
                    arguments.Add(request.PrinterName);
                }

                arguments.Add(temporaryPath);

                var result = await _processRunner.RunAsync(
                    "lp",
                    arguments,
                    cancellationToken);

                if (result.ExitCode != 0)
                {
                    return Failure(ProcessError(result));
                }
            }

            return OperationResult.Success(new PrintSummary(
                request.Pages.Length * request.Copies,
                request.Copies,
                request.PrinterName));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or System.ComponentModel.Win32Exception
                or InvalidOperationException)
        {
            return Failure(exception.Message);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private static string ProcessError(ProcessRunResult result)
    {
        var message = string.IsNullOrWhiteSpace(result.StandardError)
            ? $"The print command exited with code {result.ExitCode}."
            : result.StandardError.Trim();

        return message;
    }

    private static string EscapePowerShell(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);

    private static OperationResult<PrintSummary> Failure(string message) =>
        OperationResult.Failure<PrintSummary>(
            RenderingDiagnosticCodes.PrintFailed,
            string.IsNullOrWhiteSpace(message)
                ? "The document could not be printed."
                : message);

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
