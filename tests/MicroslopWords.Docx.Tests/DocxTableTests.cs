using System.Collections.Immutable;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using MicroslopWords.Formats.Docx;
using Shouldly;
using WpBody = DocumentFormat.OpenXml.Wordprocessing.Body;
using WpDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using WpParagraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using WpRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using WpTable = DocumentFormat.OpenXml.Wordprocessing.Table;
using WpTableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using WpTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using WpText = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace MicroslopWords.Docx.Tests;

public sealed class DocxTableTests
{
    [Fact]
    public void SaveAndLoad_round_trips_table_structure_content_and_formatting()
    {
        var table = CreateTable(3, 3, "A", "B", "C", "D", "E", "F", "G", "H", "I");
        table = table.ReplaceCell(
            table.Rows[0].Cells[0].Id,
            table.Rows[0].Cells[0] with { ShadingHex = "#ABCDEF" });
        var document = CreateDocument(table);

        using var stream = new MemoryStream();
        DocxService.Save(document, stream);
        stream.Position = 0;

        using (var package = WordprocessingDocument.Open(stream, false))
        {
            package.MainDocumentPart.ShouldNotBeNull();
            package.MainDocumentPart!.Document!.Body!
                .Elements<WpTable>()
                .ShouldNotBeEmpty();
            var errors = new OpenXmlValidator()
                .Validate(package, TestContext.Current.CancellationToken)
                .ToList();
            errors.ShouldBeEmpty(string.Join(
                "\n",
                errors.Select(error => $"{error.Path?.XPath}: {error.Description}")));
        }

        stream.Position = 0;
        var loaded = DocxService.Load(stream, "Table", TestContext.Current.CancellationToken);

        loaded.IsSuccess.ShouldBeTrue();
        var loadedTable = loaded.Value!.Sections.Single().Blocks
            .OfType<TableBlock>()
            .ShouldHaveSingleItem();

        loadedTable.Rows.Length.ShouldBe(3);
        loadedTable.ColumnCount.ShouldBe(3);
        loadedTable.HasHeaderRow.ShouldBeTrue();
        loadedTable.Rows[0].IsHeader.ShouldBeTrue();
        loadedTable.Rows[0].Cells[0].ShadingHex.ShouldBe("#ABCDEF");
        loadedTable.PlainText.ShouldContain("A");
        loadedTable.PlainText.ShouldContain("I");
        loadedTable.ColumnWidths.Sum().ShouldBe(468, 1);
    }

    [Fact]
    public void SaveAndLoad_round_trips_merged_cells()
    {
        var table = CreateTable(3, 3, "A", "B", "C", "D", "E", "F", "G", "H", "I");
        var document = CreateDocument(table);
        var first = table.Rows[0].Cells[0].Paragraphs[0];
        var second = table.Rows[0].Cells[1].Paragraphs[0];
        var horizontalSelection = new Selection(
            TextNavigation.StartOf(first),
            TextNavigation.EndOf(second));
        document = new MergeTableCellsCommand(horizontalSelection).Apply(document);
        table = document.Sections.Single().Blocks.OfType<TableBlock>().Single();
        var mergedFirst = table.Rows[0].Cells[0].Paragraphs[0];
        var mergedBelow = table.Rows[1].Cells[1].Paragraphs[0];
        var verticalSelection = new Selection(
            TextNavigation.StartOf(mergedFirst),
            TextNavigation.EndOf(mergedBelow));
        document = new MergeTableCellsCommand(verticalSelection).Apply(document);

        using var stream = new MemoryStream();
        DocxService.Save(document, stream);
        stream.Position = 0;

        using (var package = WordprocessingDocument.Open(stream, false))
        {
            var errors = new OpenXmlValidator()
                .Validate(package, TestContext.Current.CancellationToken)
                .ToList();
            errors.ShouldBeEmpty(string.Join(
                "\n",
                errors.Select(error => $"{error.Path?.XPath}: {error.Description}")));
        }

        stream.Position = 0;
        var loaded = DocxService.Load(stream, "Merged", TestContext.Current.CancellationToken);

        loaded.IsSuccess.ShouldBeTrue();
        var loadedTable = loaded.Value!.Sections.Single().Blocks
            .OfType<TableBlock>()
            .ShouldHaveSingleItem();
        var mergedCell = loadedTable.Rows[0].Cells[0];

        mergedCell.ColumnSpan.ShouldBe(2);
        mergedCell.RowSpan.ShouldBe(2);
        loadedTable.Rows[1].Cells[0].IsMergedContinuation.ShouldBeTrue();
    }

    [Fact]
    public void SaveAndLoad_round_trips_cleared_cell_borders()
    {
        var table = CreateTable(1, 1, "A");
        table = table.ReplaceCell(
            table.Rows[0].Cells[0].Id,
            table.Rows[0].Cells[0] with
            {
                Borders = TableBorders.Default.WithoutSides(),
            });
        var document = CreateDocument(table);

        using var stream = new MemoryStream();
        DocxService.Save(document, stream);
        stream.Position = 0;
        var loaded = DocxService.Load(stream, "Borders", TestContext.Current.CancellationToken);
        var loadedCell = loaded.Value!.Sections.Single().Blocks
            .OfType<TableBlock>()
            .Single()
            .Rows[0]
            .Cells[0];
        var borders = loadedCell.Borders.ShouldNotBeNull();

        borders.Left.ShouldBeFalse();
        borders.Top.ShouldBeFalse();
        borders.Right.ShouldBeFalse();
        borders.Bottom.ShouldBeFalse();
    }

    [Fact]
    public void Load_nested_table_reports_a_diagnostic()
    {
        var nested = new WpTable(
            new WpTableRow(
                new WpTableCell(
                    new WpParagraph(new WpRun(new WpText("nested"))))));
        var outer = new WpTable(
            new WpTableRow(
                new WpTableCell(
                    new WpParagraph(new WpRun(new WpText("outer"))),
                    nested)));
        using var stream = CreatePackage(outer);

        var loaded = DocxService.Load(stream, "Nested", TestContext.Current.CancellationToken);

        loaded.IsSuccess.ShouldBeTrue();
        loaded.Diagnostics.ShouldContain(diagnostic =>
            diagnostic.Code == DocxDiagnosticCodes.UnsupportedTable
            && diagnostic.Severity == DiagnosticSeverity.Warning);
    }

    private static TableBlock CreateTable(
        int rows,
        int columns,
        params string[] texts)
    {
        var table = TableBlock.Create(rows, columns, hasHeaderRow: true);
        var index = 0;
        var updatedRows = table.Rows
            .Select(row => row with
            {
                Cells = row.Cells
                    .Select(cell => cell with
                    {
                        Paragraphs = ImmutableArray.Create(
                            ParagraphBlock.Create(
                                index < texts.Length ? texts[index++] : string.Empty)),
                    })
                    .ToImmutableArray(),
            })
            .ToImmutableArray();

        return table.WithRows(updatedRows);
    }

    private static Document CreateDocument(TableBlock table)
    {
        var section = new Section(Guid.NewGuid(), ImmutableArray.Create<Block>(table));

        return new Document(Guid.NewGuid(), "Tables", ImmutableArray.Create(section));
    }

    private static MemoryStream CreatePackage(params OpenXmlElement[] blocks)
    {
        var stream = new MemoryStream();

        using (var package = WordprocessingDocument.Create(
                   stream,
                   WordprocessingDocumentType.Document,
                   autoSave: true))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new WpDocument();
            var body = new WpBody();

            foreach (var block in blocks)
            {
                body.Append(block);
            }

            main.Document.Append(body);
            main.Document.Save();
        }

        stream.Position = 0;
        return stream;
    }
}
