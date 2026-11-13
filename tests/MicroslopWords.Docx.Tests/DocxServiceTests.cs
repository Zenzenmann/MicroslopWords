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
    public void SaveAndLoad_round_trips_page_settings()
    {
        var page = PageSettings.A4 with
        {
            Orientation = PageOrientation.Landscape,
            MarginLeftPoints = 36,
            MarginTopPoints = 48,
            MarginRightPoints = 54,
            MarginBottomPoints = 60,
        };
        var paragraph = ParagraphBlock.Create("Page settings");
        var section = new Section(
            Guid.NewGuid(),
            ImmutableArray.Create<Block>(paragraph),
            page);
        var document = new Document(Guid.NewGuid(), "Page", ImmutableArray.Create(section));

        using var stream = new MemoryStream();
        DocxService.Save(document, stream);
        stream.Position = 0;

        var loaded = DocxService.Load(stream, "Page", TestContext.Current.CancellationToken);

        loaded.IsSuccess.ShouldBeTrue();

        var loadedPage = loaded.Value!.Sections.Single().EffectivePage;

        loadedPage.Orientation.ShouldBe(PageOrientation.Landscape);
        loadedPage.EffectiveWidthPoints.ShouldBe(841.89, 0.1);
        loadedPage.EffectiveHeightPoints.ShouldBe(595.28, 0.1);
        loadedPage.MarginLeftPoints.ShouldBe(36);
        loadedPage.MarginTopPoints.ShouldBe(48);
        loadedPage.MarginRightPoints.ShouldBe(54);
        loadedPage.MarginBottomPoints.ShouldBe(60);
    }

    [Fact]
    public void SaveAndLoad_round_trips_paragraph_formatting()
    {
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.Create("Indented")),
            new ParagraphProperties
            {
                Alignment = ParagraphAlignment.Center,
                SpaceBeforePoints = 12,
                SpaceAfterPoints = 4,
                LineSpacing = 1.5,
                LeftIndentPoints = 18,
                RightIndentPoints = 9,
                FirstLineIndentPoints = 6,
            });
        var section = new Section(
            Guid.NewGuid(),
            ImmutableArray.Create<Block>(paragraph));
        var document = new Document(Guid.NewGuid(), "Paragraph", ImmutableArray.Create(section));

        using var stream = new MemoryStream();
        DocxService.Save(document, stream);
        stream.Position = 0;

        var loaded = DocxService.Load(stream, "Paragraph", TestContext.Current.CancellationToken);
        var properties = loaded.Value!.Sections.Single()
            .Blocks.OfType<ParagraphBlock>()
            .Single()
            .EffectiveProperties;

        properties.Alignment.ShouldBe(ParagraphAlignment.Center);
        properties.SpaceBeforePoints.ShouldBe(12);
        properties.SpaceAfterPoints.ShouldBe(4);
        properties.LineSpacing.ShouldBe(1.5);
        properties.LeftIndentPoints.ShouldBe(18);
        properties.RightIndentPoints.ShouldBe(9);
        properties.FirstLineIndentPoints.ShouldBe(6);
    }

    [Fact]
    public void SaveAndLoad_round_trips_lists_as_structure()
    {
        var listId = Guid.NewGuid();
        var first = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.Create("One")),
            new ParagraphProperties
            {
                ListKind = ListKind.Numbered,
                ListLevel = 1,
                ListId = listId,
            });
        var second = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.Create("Nested")),
            new ParagraphProperties
            {
                ListKind = ListKind.Bulleted,
                ListLevel = 2,
                ListId = listId,
            });
        var restarted = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.Create("Restart")),
            new ParagraphProperties
            {
                ListKind = ListKind.Numbered,
                ListLevel = 1,
                ListId = Guid.NewGuid(),
            });
        var section = new Section(
            Guid.NewGuid(),
            ImmutableArray.Create<Block>(first, second, restarted));
        var document = new Document(Guid.NewGuid(), "Lists", ImmutableArray.Create(section));

        using var stream = new MemoryStream();
        DocxService.Save(document, stream);
        stream.Position = 0;

        var loaded = DocxService.Load(stream, "Lists", TestContext.Current.CancellationToken);
        var paragraphs = loaded.Value!.Sections.Single()
            .Blocks.OfType<ParagraphBlock>()
            .ToArray();

        paragraphs[0].EffectiveProperties.ListKind.ShouldBe(ListKind.Numbered);
        paragraphs[0].EffectiveProperties.ListLevel.ShouldBe(1);
        paragraphs[1].EffectiveProperties.ListKind.ShouldBe(ListKind.Bulleted);
        paragraphs[1].EffectiveProperties.ListLevel.ShouldBe(2);
        paragraphs[0].EffectiveProperties.ListId.ShouldBe(
            paragraphs[1].EffectiveProperties.ListId);
        paragraphs[2].EffectiveProperties.ListId.ShouldNotBe(
            paragraphs[0].EffectiveProperties.ListId);
    }

    [Fact]
    public void SaveAndLoad_round_trips_page_breaks()
    {
        var paragraph = ParagraphBlock.Create("Before\fAfter");
        var section = new Section(
            Guid.NewGuid(),
            ImmutableArray.Create<Block>(paragraph));
        var document = new Document(Guid.NewGuid(), "Breaks", ImmutableArray.Create(section));

        using var stream = new MemoryStream();
        DocxService.Save(document, stream);
        stream.Position = 0;

        var loaded = DocxService.Load(stream, "Breaks", TestContext.Current.CancellationToken);

        loaded.Value!.PlainText.ShouldBe("Before\fAfter");
    }
    [Fact]
    public void SaveAndLoad_round_trips_character_formatting()
    {
        var properties = new RunProperties
        {
            FontFamily = "Calibri",
            FontSizePoints = 13.5,
            Bold = true,
            Italic = true,
            Underline = true,
            Strikethrough = true,
            ColorHex = "#112233",
            HighlightHex = "#FFFF00",
        };
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.Create("Styled", properties)));
        var section = new Section(
            Guid.NewGuid(),
            ImmutableArray.Create<Block>(paragraph));
        var document = new Document(Guid.NewGuid(), "Formatting", ImmutableArray.Create(section));

        using var stream = new MemoryStream();
        DocxService.Save(document, stream);
        stream.Position = 0;

        var loaded = DocxService.Load(stream, "Formatting", TestContext.Current.CancellationToken);

        loaded.IsSuccess.ShouldBeTrue();

        var loadedProperties = loaded.Value!.Sections.Single()
            .Blocks.OfType<ParagraphBlock>()
            .Single()
            .Runs.Single()
            .EffectiveProperties;

        loadedProperties.FontFamily.ShouldBe("Calibri");
        loadedProperties.FontSizePoints.ShouldBe(13.5);
        loadedProperties.Bold.ShouldBeTrue();
        loadedProperties.Italic.ShouldBeTrue();
        loadedProperties.Underline.ShouldBeTrue();
        loadedProperties.Strikethrough.ShouldBeTrue();
        loadedProperties.ColorHex.ShouldBe("#112233");
        loadedProperties.HighlightHex.ShouldBe("#FFFF00");
    }

    [Fact]
    public void SaveAndLoad_round_trips_soft_line_breaks()
    {
        var paragraph = ParagraphBlock.Create("first\nsecond\n");
        var section = new Section(
            Guid.NewGuid(),
            ImmutableArray.Create<Block>(paragraph));
        var document = new Document(Guid.NewGuid(), "Breaks", ImmutableArray.Create(section));

        using var stream = new MemoryStream();
        DocxService.Save(document, stream);
        stream.Position = 0;

        var loaded = DocxService.Load(stream, "Breaks", TestContext.Current.CancellationToken);

        loaded.IsSuccess.ShouldBeTrue();
        loaded.Value!.PlainText.ShouldBe("first\nsecond\n");
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
    public void Load_table_imports_cells_without_unsupported_diagnostic()
    {
        var table = new DocumentFormat.OpenXml.Wordprocessing.Table(
            new DocumentFormat.OpenXml.Wordprocessing.TableRow(
                new DocumentFormat.OpenXml.Wordprocessing.TableCell(
                    new WpParagraph(new WpRun(new WpText("cell"))))));
        using var stream = CreatePackage(table);
        var loaded = DocxService.Load(stream, "Table", TestContext.Current.CancellationToken);

        loaded.IsSuccess.ShouldBeTrue();
        loaded.Value.ShouldNotBeNull();
        loaded.Value.PlainText.ShouldBe("cell");
        loaded.Value.Sections.Single().Blocks.OfType<TableBlock>().ShouldHaveSingleItem();
        loaded.Diagnostics.ShouldNotContain(diagnostic =>
            diagnostic.Code == DocxDiagnosticCodes.UnsupportedTable);
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
