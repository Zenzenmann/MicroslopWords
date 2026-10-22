using System.Text;
using MicroslopWords.App.Services;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class AtomicFileWriterTests
{
    [Fact]
    public void Write_replaces_target_with_new_content()
    {
        var directory = CreateTemporaryDirectory();
        var target = Path.Combine(directory, "document.docx");

        try
        {
            File.WriteAllText(target, "old");

            var writer = new AtomicFileWriter();
            writer.Write(
                target,
                stream =>
                {
                    var bytes = Encoding.UTF8.GetBytes("new");
                    stream.Write(bytes, 0, bytes.Length);
                },
                TestContext.Current.CancellationToken);

            File.ReadAllText(target).ShouldBe("new");
            Directory.GetFiles(directory).ShouldBe([target]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Write_failure_after_temp_creation_keeps_target_and_cleans_temp()
    {
        var directory = CreateTemporaryDirectory();
        var target = Path.Combine(directory, "document.docx");

        try
        {
            File.WriteAllText(target, "old");
            var writer = new AtomicFileWriter();

            Should.Throw<IOException>(() =>
                writer.Write(
                    target,
                    stream =>
                    {
                        var bytes = Encoding.UTF8.GetBytes("partial");
                        stream.Write(bytes, 0, bytes.Length);
                        throw new IOException("Injected write failure.");
                    },
                    TestContext.Current.CancellationToken));

            File.ReadAllText(target).ShouldBe("old");
            Directory.GetFiles(directory).ShouldBe([target]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Write_replacement_failure_keeps_target_and_cleans_temp()
    {
        var directory = CreateTemporaryDirectory();
        var target = Path.Combine(directory, "document.docx");

        try
        {
            File.WriteAllText(target, "old");
            var writer = new AtomicFileWriter(
                (_, _, _) => throw new IOException("Injected replacement failure."));

            Should.Throw<IOException>(() =>
                writer.Write(
                    target,
                    stream =>
                    {
                        var bytes = Encoding.UTF8.GetBytes("new");
                        stream.Write(bytes, 0, bytes.Length);
                    },
                    TestContext.Current.CancellationToken));

            File.ReadAllText(target).ShouldBe("old");
            Directory.GetFiles(directory).ShouldBe([target]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Write_cancellation_after_temp_creation_keeps_target_and_cleans_temp()
    {
        var directory = CreateTemporaryDirectory();
        var target = Path.Combine(directory, "document.docx");

        try
        {
            File.WriteAllText(target, "old");
            using var cancellation = new CancellationTokenSource();
            var writer = new AtomicFileWriter();

            Should.Throw<OperationCanceledException>(() =>
                writer.Write(
                    target,
                    stream =>
                    {
                        var bytes = Encoding.UTF8.GetBytes("new");
                        stream.Write(bytes, 0, bytes.Length);
                        cancellation.Cancel();
                    },
                    cancellation.Token));

            File.ReadAllText(target).ShouldBe("old");
            Directory.GetFiles(directory).ShouldBe([target]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "MicroslopWords.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
