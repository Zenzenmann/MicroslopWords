using System.Collections.Immutable;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using CoreDocument = MicroslopWords.Core.Documents.Document;
using CoreRun = MicroslopWords.Core.Documents.Run;
using WpDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using WpParagraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using WpRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using WpText = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace MicroslopWords.Formats.Docx;

public static class DocxService
{
    public static OperationResult<CoreDocument> Load(
        Stream stream,
        string? title = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var wordDocument = WordprocessingDocument.Open(stream, false);
            cancellationToken.ThrowIfCancellationRequested();

            var mainPart = wordDocument.MainDocumentPart;
            var body = mainPart?.Document?.Body;

            if (mainPart is null || body is null)
            {
                return OperationResult.Failure<CoreDocument>(
                    DocxDiagnosticCodes.InvalidPackage,
                    "The DOCX file does not contain a main document body.");
            }

            var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
            var reportedCodes = new HashSet<string>(StringComparer.Ordinal);

            void Report(string code, string message)
            {
                if (reportedCodes.Add(code))
                {
                    diagnostics.Add(new Diagnostic(code, DiagnosticSeverity.Warning, message));
                }
            }

            if (mainPart.HeaderParts.Any() || mainPart.FooterParts.Any())
            {
                Report(
                    DocxDiagnosticCodes.UnsupportedHeaderFooter,
                    "Headers and footers are not shown in this preview.");
            }

            var blocks = ImmutableArray.CreateBuilder<Block>();

            foreach (var element in body.ChildElements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                switch (element)
                {
                    case WpParagraph paragraph:
                        blocks.Add(ToParagraphBlock(paragraph, Report));
                        break;
                    case Table:
                        Report(
                            DocxDiagnosticCodes.UnsupportedTable,
                            "Tables are not shown in this preview.");
                        break;
                    case SectionProperties:
                        break;
                    default:
                        Report(
                            DocxDiagnosticCodes.UnsupportedBlock,
                            "Some block content is not shown in this preview.");
                        break;
                }
            }

            if (blocks.Count == 0)
            {
                blocks.Add(ParagraphBlock.Create());
            }

            var section = new Section(
                Guid.NewGuid(),
                blocks.ToImmutable(),
                PageSettings.Letter);
            var document = new CoreDocument(
                Guid.NewGuid(),
                title ?? "Imported document",
                ImmutableArray.Create(section));

            return OperationResult.Success(document, diagnostics.ToImmutable());
        }
        catch (OpenXmlPackageException)
        {
            return OperationResult.Failure<CoreDocument>(
                DocxDiagnosticCodes.InvalidPackage,
                "The selected file is not a readable DOCX package.");
        }
        catch (Exception exception) when (
            exception is InvalidDataException or FileFormatException)
        {
            return OperationResult.Failure<CoreDocument>(
                DocxDiagnosticCodes.InvalidPackage,
                "The selected file is not a readable DOCX package.");
        }
    }

    public static void Save(CoreDocument document, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);

        using var wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document);
        var mainPart = wordDocument.AddMainDocumentPart();
        mainPart.Document = new WpDocument();
        var body = new Body();
        mainPart.Document.Append(body);

        foreach (var paragraphBlock in document.Sections
                     .SelectMany(section => section.Blocks)
                     .OfType<ParagraphBlock>())
        {
            var paragraph = new WpParagraph();

            foreach (var run in paragraphBlock.Runs)
            {
                paragraph.Append(new WpRun(
                    new WpText(run.Text) { Space = SpaceProcessingModeValues.Preserve }));
            }

            body.Append(paragraph);
        }

        if (!body.Elements<WpParagraph>().Any())
        {
            body.Append(new WpParagraph());
        }

        mainPart.Document.Save();
    }

    private static ParagraphBlock ToParagraphBlock(
        WpParagraph paragraph,
        Action<string, string> report)
    {
        if (paragraph.Descendants<Hyperlink>().Any())
        {
            report(
                DocxDiagnosticCodes.UnsupportedHyperlink,
                "Hyperlink styling is not shown in this preview.");
        }

        if (paragraph.Descendants<Drawing>().Any() || paragraph.Descendants<Picture>().Any())
        {
            report(
                DocxDiagnosticCodes.UnsupportedImage,
                "Images are not shown in this preview.");
        }

        if (paragraph.Descendants<FieldChar>().Any()
            || paragraph.Descendants<FieldCode>().Any()
            || paragraph.Descendants<SimpleField>().Any())
        {
            report(
                DocxDiagnosticCodes.UnsupportedField,
                "Fields are not evaluated in this preview.");
        }

        if (paragraph.Descendants<InsertedRun>().Any()
            || paragraph.Descendants<DeletedRun>().Any())
        {
            report(
                DocxDiagnosticCodes.UnsupportedTrackedChange,
                "Tracked changes are not shown in this preview.");
        }

        var runs = ImmutableArray.CreateBuilder<CoreRun>();

        foreach (var run in paragraph.Descendants<WpRun>())
        {
            var text = string.Concat(run.Elements<WpText>().Select(runText => runText.Text));

            if (run.Elements<WpText>().Any()
                && run.ChildElements.Any(child =>
                    child is not WpText
                    && child is not DocumentFormat.OpenXml.Wordprocessing.RunProperties))
            {
                report(
                    DocxDiagnosticCodes.UnsupportedRunContent,
                    "Some run content is not shown in this preview.");
            }

            if (text.Length > 0)
            {
                runs.Add(new CoreRun(Guid.NewGuid(), text));
            }
        }

        return new ParagraphBlock(Guid.NewGuid(), runs.ToImmutable());
    }
}
