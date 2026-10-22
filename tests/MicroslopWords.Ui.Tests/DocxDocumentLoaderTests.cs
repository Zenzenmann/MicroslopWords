using System.Collections.Immutable;
using MicroslopWords.App.Services;
using MicroslopWords.Core.Documents;
using MicroslopWords.Formats.Docx;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class DocxDocumentLoaderTests
{
    private readonly DocxDocumentLoader _loader = new();

    [Fact]
    public void Load_valid_docx_returns_document()
    {
        var path = TempPath(".docx");
        var document = new Document(
            Guid.NewGuid(),
            "Sample",
            ImmutableArray.Create(new Section(
                Guid.NewGuid(),
                ImmutableArray.Create<Block>(ParagraphBlock.Create("Hello")))));

        try
        {
            using (var stream = File.Create(path))
            {
                DocxService.Save(document, stream);
            }

            var result = _loader.Load(path, TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value.PlainText.ShouldBe("Hello");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_missing_file_returns_file_missing_diagnostic()
    {
        var result = _loader.Load(TempPath(".docx"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Diagnostics.Single().Code.ShouldBe(DocxDiagnosticCodes.FileMissing);
    }

    [Fact]
    public void Load_non_docx_extension_returns_unsupported_extension_diagnostic()
    {
        var path = TempPath(".txt");

        try
        {
            File.WriteAllText(path, "not a document");
            var result = _loader.Load(path, TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            result.Diagnostics.Single().Code.ShouldBe(DocxDiagnosticCodes.UnsupportedExtension);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_empty_file_returns_empty_file_diagnostic()
    {
        var path = TempPath(".docx");

        try
        {
            File.WriteAllBytes(path, []);
            var result = _loader.Load(path, TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            result.Diagnostics.Single().Code.ShouldBe(DocxDiagnosticCodes.EmptyFile);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_non_zip_signature_returns_invalid_signature_diagnostic()
    {
        var path = TempPath(".docx");

        try
        {
            File.WriteAllText(path, "not a zip");
            var result = _loader.Load(path, TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            result.Diagnostics.Single().Code.ShouldBe(DocxDiagnosticCodes.InvalidSignature);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_compound_file_signature_returns_encrypted_diagnostic()
    {
        var path = TempPath(".docx");

        try
        {
            File.WriteAllBytes(path, [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]);
            var result = _loader.Load(path, TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            result.Diagnostics.Single().Code.ShouldBe(DocxDiagnosticCodes.EncryptedOrLegacyDocument);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string TempPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"msw-{Guid.NewGuid():N}{extension}");
}
