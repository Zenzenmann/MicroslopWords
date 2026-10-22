using System.Collections.Immutable;
using MicroslopWords.App.Services;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using MicroslopWords.Formats.Docx;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class DocxDocumentSaverTests
{
    [Fact]
    public void Save_replaces_target_with_reopenable_docx()
    {
        var directory = CreateTemporaryDirectory();
        var target = Path.Combine(directory, "saved document.docx");

        try
        {
            File.WriteAllText(target, "old");
            var saver = new DocxDocumentSaver();

            var result = saver.Save(
                CreateDocument("Hello saved world"),
                target,
                TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe(target);

            using var stream = File.OpenRead(target);
            var loaded = DocxService.Load(stream, "Saved", TestContext.Current.CancellationToken);
            loaded.IsSuccess.ShouldBeTrue();
            loaded.Value!.PlainText.ShouldBe("Hello saved world");
            Directory.GetFiles(directory).ShouldBe([target]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Save_maps_io_failure_to_actionable_diagnostic()
    {
        var saver = new DocxDocumentSaver(new ThrowingFileWriter());

        var result = saver.Save(
            CreateDocument("Hello"),
            "document.docx",
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Diagnostics.Single().Code.ShouldBe(DocxDiagnosticCodes.SaveFailed);
        result.Diagnostics.Single().Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public void Save_with_empty_path_fails_without_writing()
    {
        var saver = new DocxDocumentSaver(new ThrowingFileWriter());

        var result = saver.Save(
            CreateDocument("Hello"),
            " ",
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Diagnostics.Single().Code.ShouldBe(DocxDiagnosticCodes.InvalidSavePath);
    }

    private static Document CreateDocument(string text)
    {
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.Create(text)));
        var section = new Section(Guid.NewGuid(), ImmutableArray.Create<Block>(paragraph));
        return new Document(Guid.NewGuid(), "Test", ImmutableArray.Create(section));
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

    private sealed class ThrowingFileWriter : IAtomicFileWriter
    {
        public void Write(
            string targetPath,
            Action<Stream> write,
            CancellationToken cancellationToken = default) =>
            throw new IOException("Injected save failure.");
    }
}
