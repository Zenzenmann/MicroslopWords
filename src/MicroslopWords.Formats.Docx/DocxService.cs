using System.Collections.Immutable;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
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
    public static CoreDocument Load(Stream stream, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var wordDocument = WordprocessingDocument.Open(stream, false);
        var body = wordDocument.MainDocumentPart?.Document?.Body
            ?? throw new InvalidDataException("The DOCX file does not contain a main document body.");

        var blocks = body
            .Elements<WpParagraph>()
            .Select(ToParagraphBlock)
            .Cast<Block>()
            .ToImmutableArray();

        if (blocks.Length == 0)
        {
            blocks = ImmutableArray.Create<Block>(ParagraphBlock.Create());
        }

        var section = new Section(Guid.NewGuid(), blocks, PageSettings.Letter);
        return new CoreDocument(Guid.NewGuid(), title ?? "Imported document", ImmutableArray.Create(section));
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

    private static ParagraphBlock ToParagraphBlock(WpParagraph paragraph)
    {
        var runs = paragraph
            .Elements<WpRun>()
            .Select(run => new CoreRun(
                Guid.NewGuid(),
                run.GetFirstChild<WpText>()?.Text ?? string.Empty))
            .ToImmutableArray();

        return new ParagraphBlock(Guid.NewGuid(), runs);
    }
}
