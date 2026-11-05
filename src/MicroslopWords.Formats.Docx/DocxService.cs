using System.Collections.Immutable;
using System.Text;
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
            SectionProperties? sectionProperties = null;

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
                    case SectionProperties properties:
                        sectionProperties = properties;
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
                ToPageSettings(sectionProperties));
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
                var wpRun = new WpRun();
                var segments = run.Text.Split('\n');

                for (var index = 0; index < segments.Length; index++)
                {
                    if (segments[index].Length > 0 || segments.Length == 1)
                    {
                        wpRun.Append(new WpText(segments[index])
                        {
                            Space = SpaceProcessingModeValues.Preserve,
                        });
                    }

                    if (index < segments.Length - 1)
                    {
                        wpRun.Append(new Break());
                    }
                }

                paragraph.Append(wpRun);
            }

            body.Append(paragraph);
        }

        if (!body.Elements<WpParagraph>().Any())
        {
            body.Append(new WpParagraph());
        }

        body.Append(ToSectionProperties(
            document.Sections.FirstOrDefault()?.EffectivePage ?? PageSettings.Letter));

        mainPart.Document.Save();
    }

    private static PageSettings ToPageSettings(SectionProperties? properties)
    {
        var size = properties?.GetFirstChild<PageSize>();
        var margin = properties?.GetFirstChild<PageMargin>();

        var widthValue = size?.Width?.Value;
        var heightValue = size?.Height?.Value;
        var width = widthValue is > 0 ? widthValue.Value / 20.0 : PageSettings.Letter.WidthPoints;
        var height = heightValue is > 0 ? heightValue.Value / 20.0 : PageSettings.Letter.HeightPoints;
        var isLandscape = size?.Orient?.Value == PageOrientationValues.Landscape;

        return new PageSettings
        {
            WidthPoints = isLandscape ? Math.Min(width, height) : width,
            HeightPoints = isLandscape ? Math.Max(width, height) : height,
            Orientation = isLandscape ? PageOrientation.Landscape : PageOrientation.Portrait,
            MarginLeftPoints = (margin?.Left?.Value ?? 1440) / 20.0,
            MarginTopPoints = (margin?.Top?.Value ?? 1440) / 20.0,
            MarginRightPoints = (margin?.Right?.Value ?? 1440) / 20.0,
            MarginBottomPoints = (margin?.Bottom?.Value ?? 1440) / 20.0,
        };
    }

    private static SectionProperties ToSectionProperties(PageSettings page) =>
        new(
            new PageSize
            {
                Width = (uint)Math.Round(page.EffectiveWidthPoints * 20),
                Height = (uint)Math.Round(page.EffectiveHeightPoints * 20),
                Orient = page.Orientation == PageOrientation.Landscape
                    ? PageOrientationValues.Landscape
                    : PageOrientationValues.Portrait,
            },
            new PageMargin
            {
                Left = (uint)Math.Round(page.MarginLeftPoints * 20),
                Top = (int)Math.Round(page.MarginTopPoints * 20),
                Right = (uint)Math.Round(page.MarginRightPoints * 20),
                Bottom = (int)Math.Round(page.MarginBottomPoints * 20),
            });

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
            var text = new StringBuilder();

            foreach (var child in run.ChildElements)
            {
                switch (child)
                {
                    case WpText runText:
                        text.Append(runText.Text);
                        break;
                    case Break lineBreak:
                        if (lineBreak.Type is null
                            || lineBreak.Type.Value == BreakValues.TextWrapping)
                        {
                            text.Append('\n');
                        }
                        else
                        {
                            report(
                                DocxDiagnosticCodes.UnsupportedRunContent,
                                "Page and column breaks are not shown in this preview.");
                        }

                        break;
                    case CarriageReturn:
                        text.Append('\n');
                        break;
                    case TabChar:
                        text.Append('\t');
                        break;
                    case LastRenderedPageBreak:
                        break;
                    case DocumentFormat.OpenXml.Wordprocessing.RunProperties:
                        break;
                    default:
                        report(
                            DocxDiagnosticCodes.UnsupportedRunContent,
                            "Some run content is not shown in this preview.");
                        break;
                }
            }

            if (text.Length > 0)
            {
                runs.Add(new CoreRun(Guid.NewGuid(), text.ToString()));
            }
        }

        return new ParagraphBlock(Guid.NewGuid(), runs.ToImmutable());
    }
}
