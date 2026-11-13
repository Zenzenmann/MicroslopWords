using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using MicroslopWords.Layout;
using MicroslopWords.Rendering;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class PdfExporterTests
{
    [Fact]
    public void Export_writes_searchable_subset_font_pdf_with_page_geometry()
    {
        var document = CreateDocument();
        using var font = CreateFont();
        var catalog = FontCatalog.Create([font], ["TestFont"]);
        var layout = new LayoutEngine(catalog).Layout(document);
        using var stream = new MemoryStream();

        var result = PdfExporter.Export(
            layout,
            stream,
            new PdfExportOptions(new PageRange(PageRangeKind.All), Title: "Report"),
            catalog,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var bytes = stream.ToArray();
        var text = Encoding.ASCII.GetString(bytes);

        text.ShouldStartWith("%PDF");
        CountPages(text).ShouldBe(layout.Pages.Length);
        text.ShouldContain("/MediaBox");
        text.ShouldContain("612");
        text.ShouldContain("792");
        text.ShouldContain("/ToUnicode");
        text.ShouldContain("/FontFile");
    }

    [Fact]
    public void Export_honours_a_custom_page_range()
    {
        var document = CreateDocument();
        using var font = CreateFont();
        var catalog = FontCatalog.Create([font], ["TestFont"]);
        var layout = new LayoutEngine(catalog).Layout(document);
        layout.Pages.Length.ShouldBe(2);
        using var stream = new MemoryStream();

        var result = PdfExporter.Export(
            layout,
            stream,
            new PdfExportOptions(
                new PageRange(
                    PageRangeKind.Custom,
                    FromPage: 2,
                    ToPage: 2)),
            catalog,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Pages.ShouldBe([2]);
        CountPages(Encoding.ASCII.GetString(stream.ToArray())).ShouldBe(1);
    }

    [Fact]
    public void Export_rejects_an_out_of_bounds_range_without_writing()
    {
        var document = CreateDocument();
        using var font = CreateFont();
        var catalog = FontCatalog.Create([font], ["TestFont"]);
        var layout = new LayoutEngine(catalog).Layout(document);
        using var stream = new MemoryStream();

        var result = PdfExporter.Export(
            layout,
            stream,
            new PdfExportOptions(
                new PageRange(
                    PageRangeKind.Custom,
                    FromPage: 3,
                    ToPage: 9)),
            catalog,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Diagnostics.Single().Code.ShouldBe(RenderingDiagnosticCodes.InvalidPageRange);
        stream.Length.ShouldBe(0);
    }

    [Fact]
    public void Export_is_deterministic_for_identical_inputs()
    {
        var document = CreateDocument();
        using var font = CreateFont();
        var catalog = FontCatalog.Create([font], ["TestFont"]);
        var layout = new LayoutEngine(catalog).Layout(document);
        var options = new PdfExportOptions(
            new PageRange(PageRangeKind.All),
            Title: "Deterministic",
            Timestamp: new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));

        using var first = new MemoryStream();
        using var second = new MemoryStream();
        PdfExporter.Export(
            layout,
            first,
            options,
            catalog,
            TestContext.Current.CancellationToken).IsSuccess.ShouldBeTrue();
        PdfExporter.Export(
            layout,
            second,
            options,
            catalog,
            TestContext.Current.CancellationToken).IsSuccess.ShouldBeTrue();

        second.ToArray().ShouldBe(first.ToArray());
    }

    [Fact]
    public void Export_includes_headers_footers_images_and_tables()
    {
        var document = CreateDocument(includeRichContent: true);
        using var font = CreateFont();
        var catalog = FontCatalog.Create([font], ["TestFont"]);
        var layout = new LayoutEngine(catalog).Layout(document);

        layout.Pages.ShouldContain(page => page.Header != null);
        layout.Pages.ShouldContain(page => page.Footer != null);
        layout.Pages.SelectMany(page => page.Tables).ShouldNotBeEmpty();
        layout.Pages
            .SelectMany(page => page.Paragraphs)
            .SelectMany(paragraph => paragraph.Lines)
            .SelectMany(line => line.Images)
            .ShouldNotBeEmpty();

        using var stream = new MemoryStream();
        var result = PdfExporter.Export(
            layout,
            stream,
            new PdfExportOptions(new PageRange(PageRangeKind.All)),
            catalog,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var text = Encoding.ASCII.GetString(stream.ToArray());
        Regex.IsMatch(text, @"\bBI\b").ShouldBeTrue();
    }

    [Fact]
    public void Export_uses_the_document_page_size_and_orientation()
    {
        var document = CreateDocument() with
        {
            Sections =
            [
                CreateDocument().Sections[0] with
                {
                    Page = PageSettings.A4 with
                    {
                        Orientation = PageOrientation.Landscape,
                    },
                },
            ],
        };
        using var font = CreateFont();
        var catalog = FontCatalog.Create([font], ["TestFont"]);
        var layout = new LayoutEngine(catalog).Layout(document);
        using var stream = new MemoryStream();

        var result = PdfExporter.Export(
            layout,
            stream,
            new PdfExportOptions(new PageRange(PageRangeKind.All)),
            catalog,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var text = Encoding.ASCII.GetString(stream.ToArray());
        var match = Regex.Match(text, @"/MediaBox \[0 0 ([\d.]+) ([\d.]+)\]");
        match.Success.ShouldBeTrue();
        double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
            .ShouldBe(layout.Pages[0].Bounds.Width, 1);
        double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)
            .ShouldBe(layout.Pages[0].Bounds.Height, 1);
    }

    [Fact]
    public void Export_without_headers_and_footers_still_matches_the_page_count()
    {
        var document = CreateDocument();
        using var font = CreateFont();
        var catalog = FontCatalog.Create([font], ["TestFont"]);
        var layout = new LayoutEngine(catalog).Layout(document);
        using var withHeaders = new MemoryStream();
        using var withoutHeaders = new MemoryStream();

        var withResult = PdfExporter.Export(
            layout,
            withHeaders,
            new PdfExportOptions(new PageRange(PageRangeKind.All)),
            catalog,
            TestContext.Current.CancellationToken);
        var withoutResult = PdfExporter.Export(
            layout,
            withoutHeaders,
            new PdfExportOptions(
                new PageRange(PageRangeKind.All),
                IncludeHeadersFooters: false),
            catalog,
            TestContext.Current.CancellationToken);

        withResult.IsSuccess.ShouldBeTrue();
        withoutResult.IsSuccess.ShouldBeTrue();
        CountPages(Encoding.ASCII.GetString(withoutHeaders.ToArray()))
            .ShouldBe(layout.Pages.Length);
        withoutHeaders.ToArray().ShouldNotBe(withHeaders.ToArray());
    }

    private static Document CreateDocument(bool includeRichContent = false)
    {
        var header = new HeaderFooter(
        [
            new ParagraphBlock(
                Guid.NewGuid(),
                [
                    Run.Create("Header ", RunPropertiesForTest()),
                    Run.CreatePageField(PageFieldKind.PageNumber, RunPropertiesForTest()),
                    Run.Create(" of ", RunPropertiesForTest()),
                    Run.CreatePageField(PageFieldKind.TotalPages, RunPropertiesForTest()),
                ]),
        ]);
        var footer = new HeaderFooter(
        [
            new ParagraphBlock(
                Guid.NewGuid(),
                [Run.Create("Footer", RunPropertiesForTest())]),
        ]);
        var blocks = new List<Block>
        {
            new ParagraphBlock(
                Guid.NewGuid(),
                [Run.Create("First page\fSecond page", RunPropertiesForTest())]),
        };

        if (includeRichContent)
        {
            var table = TableBlock.Create(rows: 2, columns: 2);
            table = table.WithRows(
                table.Rows
                    .Select((row, rowIndex) => row with
                    {
                        Cells = row.Cells
                            .Select((cell, columnIndex) => cell with
                            {
                                Paragraphs =
                                [
                                    new ParagraphBlock(
                                        Guid.NewGuid(),
                                        [Run.Create(
                                            $"R{rowIndex}C{columnIndex}",
                                            RunPropertiesForTest())]),
                                ],
                            })
                            .ToImmutableArray(),
                    })
                    .ToImmutableArray());
            blocks.Add(table);
            blocks.Add(new ParagraphBlock(
                Guid.NewGuid(),
                [
                    Run.CreateImage(
                        new InlineImage(
                            ImageFormat.Png,
                            "red.png",
                            [.. Convert.FromBase64String(PngBase64)],
                            100,
                            50,
                            75,
                            37.5,
                            "Red rectangle"),
                        RunPropertiesForTest()),
                ]));
        }

        var section = Section.CreateEmpty()
            with
        {
            Blocks = blocks.ToImmutableArray(),
            Header = header,
            Footer = footer,
        };

        return Document.CreateEmpty("Report") with
        {
            Sections = [section],
        };
    }

    private static RunProperties RunPropertiesForTest() =>
        new() { FontFamily = "TestFont", FontSizePoints = 11 };

    private static FontFace CreateFont()
    {
        var bytes = FindSystemFont()
            ?? throw new InvalidOperationException("No test font is available.");

        return new FontFace("TestFont", FontWeight.Regular, FontSlant.Upright, bytes);
    }

    private static int CountPages(string text) =>
        Regex.Count(text, @"/Type\s*/Page\b");

    private static byte[]? FindSystemFont()
    {
        string[] candidates = OperatingSystem.IsWindows()
            ? [Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                "arial.ttf")]
            : OperatingSystem.IsMacOS()
                ? ["/System/Library/Fonts/Supplemental/Arial.ttf"]
                : ["/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"];

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return File.ReadAllBytes(candidate);
            }
        }

        return null;
    }

    private const string PngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAGQAAAAyCAIAAAAlV+npAAAAhUlEQVR4nO3SMREAIBADQUAI/qUgCwt//W6d6ib73buYOcMdYjWeFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYi15j5uMQFoB1/W+QAAAABJRU5ErkJggg==";
}
