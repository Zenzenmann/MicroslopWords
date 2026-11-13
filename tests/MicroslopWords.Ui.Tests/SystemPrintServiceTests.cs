using MicroslopWords.App.Services;
using MicroslopWords.Rendering;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class SystemPrintServiceTests
{
    [Fact]
    public async Task GetPrintersAsync_parses_the_platform_output()
    {
        var output = OperatingSystem.IsWindows()
            ? "Office\nLab"
            : "Office accepting requests since Jan 1\nLab accepting requests since Jan 2";
        var runner = new FakeProcessRunner
        {
            Result = new ProcessRunResult(0, output, string.Empty),
        };
        var service = new SystemPrintService(runner);

        var printers = await service.GetPrintersAsync(
            TestContext.Current.CancellationToken);

        printers.ShouldBe(["Lab", "Office"]);
    }

    [Fact]
    public async Task PrintAsync_writes_a_temporary_pdf_and_deletes_it()
    {
        var directory = CreateTemporaryDirectory();
        var runner = new FakeProcessRunner();
        var service = new SystemPrintService(runner, directory);
        var request = new PrintRequest(
            "Report",
            [1, 2, 3],
            [1, 2],
            Copies: 2,
            PrinterName: "Office");

        try
        {
            var result = await service.PrintAsync(
                request,
                TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value!.PageCount.ShouldBe(4);
            result.Value.Copies.ShouldBe(2);
            Directory.GetFiles(directory, "*.pdf").ShouldBeEmpty();
            runner.Calls.ShouldNotBeEmpty();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task PrintAsync_returns_an_actionable_failure_when_the_command_fails()
    {
        var directory = CreateTemporaryDirectory();
        var runner = new FakeProcessRunner
        {
            Result = new ProcessRunResult(1, string.Empty, "The printer is offline."),
        };
        var service = new SystemPrintService(runner, directory);

        try
        {
            var result = await service.PrintAsync(
                new PrintRequest("Report", [1], [1], 1, "Office"),
                TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            result.Diagnostics.Single().Code.ShouldBe(RenderingDiagnosticCodes.PrintFailed);
            result.Diagnostics.Single().Message.ShouldBe("The printer is offline.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task PrintAsync_rejects_an_invalid_copy_count_before_writing()
    {
        var directory = CreateTemporaryDirectory();
        var runner = new FakeProcessRunner();
        var service = new SystemPrintService(runner, directory);

        try
        {
            var result = await service.PrintAsync(
                new PrintRequest("Report", [1], [1], 0, null),
                TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            runner.Calls.ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"msw-print-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public ProcessRunResult Result { get; set; } = new(0, string.Empty, string.Empty);

        public List<(string FileName, IReadOnlyList<string> Arguments)> Calls { get; } = [];

        public Task<ProcessRunResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((fileName, arguments));

            var last = arguments.Count > 0 ? arguments[^1] : null;

            if (last is not null
                && last.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                File.Exists(last).ShouldBeTrue();
            }

            return Task.FromResult(Result);
        }
    }
}
