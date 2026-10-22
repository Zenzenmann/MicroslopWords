using System.Collections.Immutable;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using MicroslopWords.Formats.Docx;
using Shouldly;
using WpDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using WpParagraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using WpRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using WpText = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace MicroslopWords.Docx.Tests;

public sealed class DocxServiceTests
{
    [Fact]
    public void SaveAndLoad_round_trips_paragraph_text()
    {
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.Create("Hello world")));
        var section = new Section(
            Guid.NewGuid(),
            ImmutableArray.Create<Block>(paragraph));
        var document = new Document(Guid.NewGuid(), "Test", ImmutableArray.Create(section));

        using var stream = new MemoryStream();
        DocxService.Save(document, stream);
        stream.Position = 0;

        var loaded = DocxService.Load(stream, "Test", TestContext.Current.CancellationToken);

        loaded.IsSuccess.ShouldBeTrue();
        loaded.Value.ShouldNotBeNull();
        loaded.Value.Title.ShouldBe("Test");
        loaded.Value.PlainText.ShouldBe("Hello world");
    }

    [Fact]
    public void Load_imports_unicode_text()
    {
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.Create("Hello \u4e16\u754c \ud83d\ude80")));
        var section = new Section(Guid.NewGuid(), ImmutableArray.Create<Block>(paragraph));
        var document = new Document(Guid.NewGuid(), "Unicode", ImmutableArray.Create(section));

        using var stream = new MemoryStream();
        DocxService.Save(document, stream);
        stream.Position = 0;

        var loaded = DocxService.Load(stream, "Unicode", TestContext.Current.CancellationToken);

        loaded.IsSuccess.ShouldBeTrue();
        loaded.Value!.PlainText.ShouldBe("Hello \u4e16\u754c \ud83d\ude80");
    }

    [Fact]
    public void Load_empty_body_creates_one_empty_paragraph()
    {
        using var stream = CreatePackage();
        var loaded = DocxService.Load(stream, "Empty", TestContext.Current.CancellationToken);

        loaded.IsSuccess.ShouldBeTrue();
        loaded.Value.ShouldNotBeNull();
        loaded.Value.Sections.Single().Blocks.Length.ShouldBe(1);
        loaded.Value.PlainText.ShouldBeEmpty();
    }

    [Fact]
    public void Load_unsupported_table_reports_stable_diagnostic()
    {
        var table = new DocumentFormat.OpenXml.Wordprocessing.Table(
            new DocumentFormat.OpenXml.Wordprocessing.TableRow(
                new DocumentFormat.OpenXml.Wordprocessing.TableCell(
                    new WpParagraph(new WpRun(new WpText("cell"))))));
        using var stream = CreatePackage(table);
        var loaded = DocxService.Load(stream, "Table", TestContext.Current.CancellationToken);

        loaded.IsSuccess.ShouldBeTrue();
        loaded.Value.ShouldNotBeNull();
        loaded.Value.PlainText.ShouldBeEmpty();
        loaded.Diagnostics.ShouldContain(diagnostic =>
            diagnostic.Code == DocxDiagnosticCodes.UnsupportedTable
            && diagnostic.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Load_unsupported_hyperlink_reports_stable_diagnostic()
    {
        var hyperlink = new DocumentFormat.OpenXml.Wordprocessing.Hyperlink(
            new WpRun(new WpText("link")));
        using var stream = CreatePackage(new WpParagraph(hyperlink));
        var loaded = DocxService.Load(stream, "Link", TestContext.Current.CancellationToken);

        loaded.IsSuccess.ShouldBeTrue();
        loaded.Value!.PlainText.ShouldBe("link");
        loaded.Diagnostics.ShouldContain(diagnostic =>
            diagnostic.Code == DocxDiagnosticCodes.UnsupportedHyperlink);
    }

    [Fact]
    public void Load_malformed_package_returns_failure()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not a docx package"));

        var loaded = DocxService.Load(stream, "Broken", TestContext.Current.CancellationToken);

        loaded.IsSuccess.ShouldBeFalse();
        loaded.Diagnostics.Single().Code.ShouldBe(DocxDiagnosticCodes.InvalidPackage);
    }

    [Fact]
    public void Load_canceled_token_throws_operation_canceled()
    {
        using var stream = CreatePackage();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Should.Throw<OperationCanceledException>(() =>
            DocxService.Load(stream, "Canceled", cancellation.Token));
    }

    private static MemoryStream CreatePackage(params DocumentFormat.OpenXml.OpenXmlElement[] bodyElements)
    {
        var stream = new MemoryStream();

        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = package.AddMainDocumentPart();
            mainPart.Document = new WpDocument();
            var body = new DocumentFormat.OpenXml.Wordprocessing.Body();

            foreach (var element in bodyElements)
            {
                body.Append(element);
            }

            mainPart.Document.Append(body);
            mainPart.Document.Save();
        }

        stream.Position = 0;
        return stream;
    }
}
