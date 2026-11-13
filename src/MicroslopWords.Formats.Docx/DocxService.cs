using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Styles;
using A = DocumentFormat.OpenXml.Drawing;
using CoreDocument = MicroslopWords.Core.Documents.Document;
using CoreParagraphProperties = MicroslopWords.Core.Documents.ParagraphProperties;
using CoreRun = MicroslopWords.Core.Documents.Run;
using CoreRunProperties = MicroslopWords.Core.Documents.RunProperties;
using CoreTableBlock = MicroslopWords.Core.Documents.TableBlock;
using CoreTableBorders = MicroslopWords.Core.Documents.TableBorders;
using CoreTableCell = MicroslopWords.Core.Documents.TableCell;
using CoreTableRow = MicroslopWords.Core.Documents.TableRow;
using Dwp = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using Pic = DocumentFormat.OpenXml.Drawing.Pictures;
using WpDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using WpDrawing = DocumentFormat.OpenXml.Wordprocessing.Drawing;
using WpParagraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using WpParagraphProperties = DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties;
using WpRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using WpRunProperties = DocumentFormat.OpenXml.Wordprocessing.RunProperties;
using WpStyle = DocumentFormat.OpenXml.Wordprocessing.Style;
using WpTable = DocumentFormat.OpenXml.Wordprocessing.Table;
using WpTableBorders = DocumentFormat.OpenXml.Wordprocessing.TableBorders;
using WpTableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using WpTableCellBorders = DocumentFormat.OpenXml.Wordprocessing.TableCellBorders;
using WpTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using WpText = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace MicroslopWords.Formats.Docx;

public static class DocxService
{
    public static OperationResult<CoreDocument> Load(
        Stream stream,
        string? title = null,
        CancellationToken cancellationToken = default
    )
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
                    "The DOCX file does not contain a main document body."
                );
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
            var styleSheet = LoadStyles(mainPart, Report);
            var blocks = ImmutableArray.CreateBuilder<Block>();
            var sections = ImmutableArray.CreateBuilder<Section>();
            var numbering = mainPart.NumberingDefinitionsPart?.Numbering;
            var listIds = new Dictionary<long, Guid>();
            var images = new Dictionary<string, ImportedImage>(StringComparer.Ordinal);
            SectionProperties? sectionProperties = null;

            void FlushSection(SectionProperties? properties)
            {
                if (blocks.Count == 0)
                {
                    blocks.Add(ParagraphBlock.Create());
                }

                sections.Add(
                    ToSection(
                        blocks.ToImmutable(),
                        properties,
                        sections.Count,
                        mainPart,
                        images,
                        Report,
                        numbering,
                        listIds
                    )
                );
                blocks.Clear();
            }

            foreach (var element in body.ChildElements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (element)
                {
                    case WpParagraph paragraph:
                        blocks.Add(
                            ToParagraphBlock(
                                paragraph,
                                mainPart,
                                images,
                                Report,
                                numbering,
                                listIds
                            )
                        );
                        if (paragraph.ParagraphProperties?.SectionProperties is { } paragraphSection)
                        {
                            FlushSection(paragraphSection);
                        }
                        break;
                    case WpTable table:
                        blocks.Add(
                            ToTableBlock(table, mainPart, images, Report, numbering, listIds)
                        );
                        break;
                    case SectionProperties properties:
                        sectionProperties = properties;
                        break;
                    default:
                        Report(
                            DocxDiagnosticCodes.UnsupportedBlock,
                            "Some block content is not shown in this preview."
                        );
                        break;
                }
            }

            FlushSection(sectionProperties);
            var document = new CoreDocument(
                Guid.NewGuid(),
                title ?? "Imported document",
                sections.ToImmutable(),
                styleSheet
            );
            return OperationResult.Success(document, diagnostics.ToImmutable());
        }
        catch (OpenXmlPackageException)
        {
            return OperationResult.Failure<CoreDocument>(
                DocxDiagnosticCodes.InvalidPackage,
                "The selected file is not a readable DOCX package."
            );
        }
        catch (Exception exception) when (exception is InvalidDataException or FileFormatException)
        {
            return OperationResult.Failure<CoreDocument>(
                DocxDiagnosticCodes.InvalidPackage,
                "The selected file is not a readable DOCX package."
            );
        }
    }

    public static void Save(CoreDocument document, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);
        using var wordDocument = WordprocessingDocument.Create(
            stream,
            WordprocessingDocumentType.Document
        );
        var mainPart = wordDocument.AddMainDocumentPart();
        mainPart.Document = new WpDocument();
        var body = new Body();
        mainPart.Document.Append(body);
        SaveStyles(mainPart, document.EffectiveStyles);
        var numberingIds = PrepareNumbering(mainPart, document);
        var imageRelationships = new Dictionary<string, string>(StringComparer.Ordinal);
        var nextDrawingId = 1u;
        var sections = document.Sections.Length == 0
            ? ImmutableArray.Create(Section.CreateEmpty())
            : document.Sections;

        for (var sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
        {
            var section = sections[sectionIndex];
            WpParagraph? lastParagraph = null;

            foreach (var block in section.Blocks)
            {
                if (block is ParagraphBlock paragraphBlock)
                {
                    lastParagraph = AppendParagraph(
                        body,
                        paragraphBlock,
                        numberingIds,
                        mainPart,
                        imageRelationships,
                        ref nextDrawingId
                    );
                }
                else if (block is CoreTableBlock tableBlock)
                {
                    body.Append(
                        ToWordTable(
                            tableBlock,
                            numberingIds,
                            mainPart,
                            imageRelationships,
                            ref nextDrawingId
                        )
                    );
                    lastParagraph = null;
                }
            }

            var sectionProperties = ToSectionProperties(
                section,
                sectionIndex,
                mainPart,
                numberingIds,
                ref nextDrawingId
            );

            if (sectionIndex < sections.Length - 1)
            {
                if (lastParagraph is null)
                {
                    lastParagraph = new WpParagraph();
                    body.Append(lastParagraph);
                }

                lastParagraph.ParagraphProperties ??= new WpParagraphProperties();
                lastParagraph.ParagraphProperties.Append(sectionProperties);
            }
            else
            {
                body.Append(sectionProperties);
            }
        }

        if (!body.Elements<WpParagraph>().Any() && !body.Elements<WpTable>().Any())
        {
            body.Append(new WpParagraph());
        }

        mainPart.Document.Save();
    }

    private static WpParagraph AppendParagraph(
        OpenXmlElement parent,
        ParagraphBlock paragraphBlock,
        Dictionary<Guid, int> numberingIds,
        OpenXmlPart ownerPart,
        Dictionary<string, string> imageRelationships,
        ref uint nextDrawingId
    )
    {
        var paragraph = new WpParagraph();
        var wordParagraphProperties = ToWordParagraphProperties(
            paragraphBlock.EffectiveProperties,
            paragraphBlock.Id,
            numberingIds
        );
        if (wordParagraphProperties is not null)
        {
            paragraph.Append(wordParagraphProperties);
        }
        foreach (var run in paragraphBlock.Runs)
        {
            if (run.PageField is { } pageField)
            {
                var simpleField = new SimpleField
                {
                    Instruction = pageField == PageFieldKind.PageNumber
                        ? " PAGE "
                        : " NUMPAGES ",
                };
                var fieldRun = new WpRun();
                var fieldProperties = ToWordRunProperties(run.Properties);

                if (fieldProperties is not null)
                {
                    fieldRun.Append(fieldProperties);
                }

                fieldRun.Append(
                    new WpText("1")
                    {
                        Space = SpaceProcessingModeValues.Preserve,
                    });
                simpleField.Append(fieldRun);
                paragraph.Append(simpleField);
                continue;
            }

            var wpRun = new WpRun();
            var wordProperties = ToWordRunProperties(run.Properties);
            if (wordProperties is not null)
            {
                wpRun.Append(wordProperties);
            }
            if (run.EffectiveImage is { } image)
            {
                if (!image.IsPlaceholder)
                {
                    var relationshipId = GetOrCreateImagePart(ownerPart, image, imageRelationships);
                    wpRun.Append(BuildInlineImage(image, relationshipId, nextDrawingId++));
                }
            }
            else
            {
                AppendRunText(wpRun, run.Text);
            }
            paragraph.Append(wpRun);
        }
        parent.Append(paragraph);
        return paragraph;
    }

    private static WpTable ToWordTable(
        CoreTableBlock table,
        Dictionary<Guid, int> numberingIds,
        OpenXmlPart ownerPart,
        Dictionary<string, string> imageRelationships,
        ref uint nextDrawingId
    )
    {
        var borders = table.Borders ?? CoreTableBorders.Default;
        var width = table.WidthPoints ?? table.ColumnWidths.Sum();
        var wordTable = new WpTable(
            new TableProperties(
                new TableWidth { Width = ToTwips(width), Type = TableWidthUnitValues.Dxa },
                ToWordTableBorders(borders),
                new TableLayout { Type = TableLayoutValues.Fixed }
            )
        );
        var grid = new TableGrid();
        foreach (var columnWidth in table.ColumnWidths)
        {
            grid.Append(new GridColumn { Width = ToTwips(columnWidth) });
        }
        wordTable.Append(grid);
        foreach (var row in table.Rows)
        {
            var wordRow = new WpTableRow();
            if (row.IsHeader)
            {
                wordRow.Append(new TableRowProperties(new TableHeader()));
            }
            var gridColumn = 0;
            foreach (var cell in row.Cells)
            {
                var span = Math.Max(1, cell.ColumnSpan);
                var cellWidth = table.ColumnWidths.Skip(gridColumn).Take(span).Sum();
                var cellProperties = new TableCellProperties(
                    new TableCellWidth
                    {
                        Width = ToTwips(cellWidth),
                        Type = TableWidthUnitValues.Dxa,
                    }
                );
                if (span > 1)
                {
                    cellProperties.Append(new GridSpan { Val = span });
                }
                if (cell.IsMergedContinuation)
                {
                    cellProperties.Append(new VerticalMerge { Val = MergedCellValues.Continue });
                }
                else if (cell.RowSpan > 1)
                {
                    cellProperties.Append(new VerticalMerge { Val = MergedCellValues.Restart });
                }
                if (cell.Borders is not null)
                {
                    cellProperties.Append(ToWordCellBorders(cell.Borders));
                }
                if (cell.ShadingHex is not null)
                {
                    cellProperties.Append(
                        new Shading
                        {
                            Val = ShadingPatternValues.Clear,
                            Fill = cell.ShadingHex.TrimStart('#'),
                        }
                    );
                }
                var wordCell = new WpTableCell(cellProperties);
                foreach (var paragraph in cell.Paragraphs)
                {
                    AppendParagraph(
                        wordCell,
                        paragraph,
                        numberingIds,
                        ownerPart,
                        imageRelationships,
                        ref nextDrawingId
                    );
                }
                wordRow.Append(wordCell);
                gridColumn += span;
            }
            wordTable.Append(wordRow);
        }
        return wordTable;
    }

    private static WpTableBorders ToWordTableBorders(CoreTableBorders borders) =>
        new(
            ToWordBorder<TopBorder>(borders, borders.Top),
            ToWordBorder<LeftBorder>(borders, borders.Left),
            ToWordBorder<BottomBorder>(borders, borders.Bottom),
            ToWordBorder<RightBorder>(borders, borders.Right)
        );

    private static WpTableCellBorders ToWordCellBorders(CoreTableBorders borders) =>
        new(
            ToWordBorder<TopBorder>(borders, borders.Top),
            ToWordBorder<LeftBorder>(borders, borders.Left),
            ToWordBorder<BottomBorder>(borders, borders.Bottom),
            ToWordBorder<RightBorder>(borders, borders.Right)
        );

    private static TBorder ToWordBorder<TBorder>(CoreTableBorders borders, bool visible)
        where TBorder : BorderType, new() =>
        new()
        {
            Val = visible ? BorderValues.Single : BorderValues.Nil,
            Color = borders.ColorHex.TrimStart('#'),
            Size = (uint)Math.Round(borders.WidthPoints * 8),
        };

    private static string GetOrCreateImagePart(
        OpenXmlPart ownerPart,
        InlineImage image,
        Dictionary<string, string> relationships
    )
    {
        var hash = Convert.ToHexString(SHA256.HashData(image.Data.AsSpan()));
        var key = $"{image.Format}:{hash}";
        if (relationships.TryGetValue(key, out var existing))
        {
            return existing;
        }
        var partType = image.Format switch
        {
            ImageFormat.Png => ImagePartType.Png,
            ImageFormat.Jpeg => ImagePartType.Jpeg,
            ImageFormat.Gif => ImagePartType.Gif,
            _ => ImagePartType.Png,
        };
        var part = AddImagePart(ownerPart, partType);
        using (var stream = new MemoryStream(image.Data.ToArray(), writable: false))
        {
            part.FeedData(stream);
        }
        var relationshipId = ownerPart.GetIdOfPart(part);
        relationships[key] = relationshipId;
        return relationshipId;
    }

    private static ImagePart AddImagePart(OpenXmlPart ownerPart, PartTypeInfo partType) =>
        ownerPart switch
        {
            MainDocumentPart mainPart => mainPart.AddImagePart(partType),
            HeaderPart headerPart => headerPart.AddImagePart(partType),
            FooterPart footerPart => footerPart.AddImagePart(partType),
            _ => throw new InvalidOperationException(
                "Images are not supported in this package part."
            ),
        };

    private static WpDrawing BuildInlineImage(
        InlineImage image,
        string relationshipId,
        uint drawingId
    )
    {
        var width = ToEmu(image.WidthPoints);
        var height = ToEmu(image.HeightPoints);
        var name = Truncate(image.FileName, 255);
        var description = Truncate(image.AltText, 255);
        var inline = new Dwp.Inline(
            new Dwp.Extent { Cx = width, Cy = height },
            new Dwp.DocProperties
            {
                Id = drawingId,
                Name = name,
                Description = description,
            },
            new Dwp.NonVisualGraphicFrameDrawingProperties(
                new A.GraphicFrameLocks { NoChangeAspect = image.LockAspectRatio }
            ),
            new A.Graphic(
                new A.GraphicData(
                    new Pic.Picture(
                        new Pic.NonVisualPictureProperties(
                            new Pic.NonVisualDrawingProperties
                            {
                                Id = drawingId,
                                Name = name,
                                Description = description,
                            },
                            new Pic.NonVisualPictureDrawingProperties(
                                new A.PictureLocks { NoChangeAspect = image.LockAspectRatio }
                            )
                        ),
                        new Pic.BlipFill(
                            new A.Blip { Embed = relationshipId },
                            new A.Stretch(new A.FillRectangle())
                        ),
                        new Pic.ShapeProperties(
                            new A.Transform2D(
                                new A.Offset { X = 0L, Y = 0L },
                                new A.Extents { Cx = width, Cy = height }
                            ),
                            new A.PresetGeometry(new A.AdjustValueList())
                            {
                                Preset = A.ShapeTypeValues.Rectangle,
                            }
                        )
                    )
                )
                {
                    Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture",
                }
            )
        )
        {
            DistanceFromTop = 0U,
            DistanceFromBottom = 0U,
            DistanceFromLeft = 0U,
            DistanceFromRight = 0U,
        };
        return new WpDrawing(inline);
    }

    private static long ToEmu(double points) => Math.Max(1, (long)Math.Round(points * 12700));

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length];

    private static Dictionary<Guid, int> PrepareNumbering(
        MainDocumentPart mainPart,
        CoreDocument document
    )
    {
        var ids = new Dictionary<Guid, int>();
        var levelKinds = new Dictionary<Guid, ListKind?[]>();
        var nextId = 1;
        foreach (var paragraph in document.EnumerateParagraphs())
        {
            var properties = StyleResolver.ResolveParagraph(paragraph, document.EffectiveStyles);
            if (properties.ListKind == ListKind.None)
            {
                continue;
            }
            var key = properties.ListId ?? paragraph.Id;
            if (!ids.ContainsKey(key))
            {
                ids[key] = nextId++;
                levelKinds[key] = new ListKind?[9];
            }
            var level = Math.Clamp(properties.ListLevel, 1, 9);
            levelKinds[key][level - 1] ??= properties.ListKind;
        }
        if (ids.Count == 0)
        {
            return ids;
        }
        var numbering = new Numbering();
        foreach (var (key, numberId) in ids)
        {
            var abstractNum = new AbstractNum { AbstractNumberId = numberId };
            var kinds = levelKinds[key];
            for (var level = 1; level <= 9; level++)
            {
                abstractNum.Append(
                    CreateNumberingLevel(level, kinds[level - 1] ?? ListKind.Numbered)
                );
            }
            numbering.Append(abstractNum);
            var instance = new NumberingInstance { NumberID = numberId };
            instance.Append(new AbstractNumId { Val = numberId });
            numbering.Append(instance);
        }
        var part = mainPart.AddNewPart<NumberingDefinitionsPart>();
        part.Numbering = numbering;
        part.Numbering.Save();
        return ids;
    }

    private static Level CreateNumberingLevel(int level, ListKind kind)
    {
        var numberingLevel = new Level { LevelIndex = level - 1 };
        numberingLevel.Append(new StartNumberingValue { Val = 1 });
        numberingLevel.Append(
            new NumberingFormat
            {
                Val =
                    kind == ListKind.Bulleted
                        ? NumberFormatValues.Bullet
                        : NumberFormatValues.Decimal,
            }
        );
        numberingLevel.Append(
            new LevelText { Val = kind == ListKind.Bulleted ? BulletForLevel(level) : $"%{level}." }
        );
        numberingLevel.Append(new LevelJustification { Val = LevelJustificationValues.Left });
        numberingLevel.Append(
            new PreviousParagraphProperties(
                new Indentation
                {
                    Left = (level * 360).ToString(CultureInfo.InvariantCulture),
                    Hanging = "360",
                }
            )
        );
        return numberingLevel;
    }

    private static string BulletForLevel(int level) =>
        level switch
        {
            1 => "\u2022",
            2 => "\u25E6",
            3 => "\u25AA",
            4 => "\u2013",
            5 => "\u00B7",
            6 => "\u25CB",
            7 => "\u25A0",
            8 => "\u25B8",
            _ => "\u2023",
        };

    private static void AppendRunText(WpRun run, string text)
    {
        var chunk = new StringBuilder();
        var wroteBreak = false;
        foreach (var character in text)
        {
            if (character is not (TextBreaks.SoftBreak or TextBreaks.PageBreak))
            {
                chunk.Append(character);
                continue;
            }
            if (chunk.Length > 0)
            {
                run.Append(
                    new WpText(chunk.ToString()) { Space = SpaceProcessingModeValues.Preserve }
                );
                chunk.Clear();
            }
            run.Append(
                character == TextBreaks.PageBreak
                    ? new Break { Type = BreakValues.Page }
                    : new Break()
            );
            wroteBreak = true;
        }
        if (chunk.Length > 0 || !wroteBreak)
        {
            run.Append(new WpText(chunk.ToString()) { Space = SpaceProcessingModeValues.Preserve });
        }
    }

    private static WpParagraphProperties? ToWordParagraphProperties(
        CoreParagraphProperties properties,
        Guid paragraphId,
        Dictionary<Guid, int> numberingIds
    )
    {
        var wordProperties = new WpParagraphProperties();
        var hasContent = false;
        var hasStyle = !string.IsNullOrWhiteSpace(properties.StyleId);
        bool Writes(ParagraphPropertyOverrides flag) =>
            !hasStyle || properties.Overrides.HasFlag(flag);
        if (!string.IsNullOrWhiteSpace(properties.StyleId))
        {
            wordProperties.Append(new ParagraphStyleId { Val = properties.StyleId });
            hasContent = true;
        }
        if (properties.ListKind != ListKind.None && Writes(ParagraphPropertyOverrides.List))
        {
            var key = properties.ListId ?? paragraphId;
            if (numberingIds.TryGetValue(key, out var numberId))
            {
                wordProperties.Append(
                    new NumberingProperties(
                        new NumberingLevelReference
                        {
                            Val = Math.Clamp(properties.ListLevel, 1, 9) - 1,
                        },
                        new NumberingId { Val = numberId }
                    )
                );
                hasContent = true;
            }
        }
        var writeBefore = Writes(ParagraphPropertyOverrides.SpaceBefore);
        var writeAfter = Writes(ParagraphPropertyOverrides.SpaceAfter);
        var writeLine = Writes(ParagraphPropertyOverrides.LineSpacing);
        var writeSpacing = hasStyle
            ? writeBefore || writeAfter || writeLine
            : properties.SpaceBeforePoints != 0
                || properties.SpaceAfterPoints != 8
                || Math.Abs(properties.LineSpacing - 1.15) > 0.0001;
        if (writeSpacing)
        {
            var spacing = new SpacingBetweenLines();
            if (writeBefore)
            {
                spacing.Before = ToTwips(properties.SpaceBeforePoints);
            }
            if (writeAfter)
            {
                spacing.After = ToTwips(properties.SpaceAfterPoints);
            }
            if (writeLine)
            {
                spacing.Line = ((int)Math.Round(properties.LineSpacing * 240)).ToString(
                    CultureInfo.InvariantCulture
                );
                spacing.LineRule = LineSpacingRuleValues.Auto;
            }
            wordProperties.Append(spacing);
            hasContent = true;
        }
        var writeLeft = Writes(ParagraphPropertyOverrides.LeftIndent);
        var writeRight = Writes(ParagraphPropertyOverrides.RightIndent);
        var writeFirstLine = Writes(ParagraphPropertyOverrides.FirstLineIndent);
        var writeIndent = hasStyle
            ? writeLeft || writeRight || writeFirstLine
            : properties.LeftIndentPoints != 0
                || properties.RightIndentPoints != 0
                || properties.FirstLineIndentPoints != 0;
        if (writeIndent)
        {
            var indentation = new Indentation();
            if (writeLeft)
            {
                indentation.Left = ToTwips(properties.LeftIndentPoints);
            }
            if (writeRight)
            {
                indentation.Right = ToTwips(properties.RightIndentPoints);
            }
            if (writeFirstLine)
            {
                if (properties.FirstLineIndentPoints >= 0)
                {
                    indentation.FirstLine = ToTwips(properties.FirstLineIndentPoints);
                }
                else
                {
                    indentation.Hanging = ToTwips(-properties.FirstLineIndentPoints);
                }
            }
            wordProperties.Append(indentation);
            hasContent = true;
        }
        if (
            Writes(ParagraphPropertyOverrides.Alignment)
            && properties.Alignment != ParagraphAlignment.Left
        )
        {
            wordProperties.Append(
                new Justification
                {
                    Val = properties.Alignment switch
                    {
                        ParagraphAlignment.Center => JustificationValues.Center,
                        ParagraphAlignment.Right => JustificationValues.Right,
                        ParagraphAlignment.Justify => JustificationValues.Both,
                        _ => JustificationValues.Left,
                    },
                }
            );
            hasContent = true;
        }
        return hasContent ? wordProperties : null;
    }

    private static string ToTwips(double points) =>
        ((int)Math.Round(points * 20)).ToString(CultureInfo.InvariantCulture);

    private static Section ToSection(
        ImmutableArray<Block> blocks,
        SectionProperties? properties,
        int sectionIndex,
        MainDocumentPart mainPart,
        Dictionary<string, ImportedImage> images,
        Action<string, string> report,
        Numbering? numbering,
        Dictionary<long, Guid> listIds)
    {
        HeaderFooter? header = null;
        HeaderFooter? firstPageHeader = null;
        HeaderFooter? footer = null;
        HeaderFooter? firstPageFooter = null;

        foreach (var reference in properties?.Elements<HeaderReference>() ?? [])
        {
            var type = reference.Type?.Value;

            if (type == HeaderFooterValues.Even)
            {
                report(
                    DocxDiagnosticCodes.UnsupportedHeaderFooter,
                    "Even-page headers are not supported."
                );
                continue;
            }

            if (FindRelatedPart(mainPart, reference.Id?.Value) is not HeaderPart part)
            {
                continue;
            }

            if (part.Header?.Elements<WpTable>().Any() == true)
            {
                report(
                    DocxDiagnosticCodes.UnsupportedTable,
                    "Tables in headers and footers are not supported."
                );
            }

            var content = ToHeaderFooter(
                part,
                part.Header?.Elements<WpParagraph>() ?? [],
                images,
                report,
                numbering,
                listIds);

            if (type == HeaderFooterValues.First)
            {
                firstPageHeader = content;
            }
            else
            {
                header = content;
            }
        }

        foreach (var reference in properties?.Elements<FooterReference>() ?? [])
        {
            var type = reference.Type?.Value;

            if (type == HeaderFooterValues.Even)
            {
                report(
                    DocxDiagnosticCodes.UnsupportedHeaderFooter,
                    "Even-page footers are not supported."
                );
                continue;
            }

            if (FindRelatedPart(mainPart, reference.Id?.Value) is not FooterPart part)
            {
                continue;
            }

            if (part.Footer?.Elements<WpTable>().Any() == true)
            {
                report(
                    DocxDiagnosticCodes.UnsupportedTable,
                    "Tables in headers and footers are not supported."
                );
            }

            var content = ToHeaderFooter(
                part,
                part.Footer?.Elements<WpParagraph>() ?? [],
                images,
                report,
                numbering,
                listIds);

            if (type == HeaderFooterValues.First)
            {
                firstPageFooter = content;
            }
            else
            {
                footer = content;
            }
        }

        var pageNumber = properties?.GetFirstChild<PageNumberType>();

        return new Section(
            Guid.NewGuid(),
            blocks,
            ToPageSettings(properties),
            header,
            footer,
            firstPageHeader,
            firstPageFooter,
            properties?.GetFirstChild<TitlePage>() is not null,
            header is null && firstPageHeader is null && sectionIndex > 0,
            footer is null && firstPageFooter is null && sectionIndex > 0,
            pageNumber?.Start?.Value,
            ToPageNumberFormat(pageNumber?.Format?.Value));
    }

    private static HeaderFooter? ToHeaderFooter(
        OpenXmlPart part,
        IEnumerable<WpParagraph> paragraphs,
        Dictionary<string, ImportedImage> images,
        Action<string, string> report,
        Numbering? numbering,
        Dictionary<long, Guid> listIds)
    {
        var blocks = paragraphs
            .Select(paragraph => (Block)ToParagraphBlock(
                paragraph,
                part,
                images,
                report,
                numbering,
                listIds))
            .ToImmutableArray();

        return blocks.Length == 0 ? null : new HeaderFooter(blocks);
    }

    private static OpenXmlPart? FindRelatedPart(
        MainDocumentPart mainPart,
        string? relationshipId)
    {
        if (string.IsNullOrEmpty(relationshipId))
        {
            return null;
        }

        try
        {
            return mainPart.GetPartById(relationshipId);
        }
        catch (Exception exception) when (
            exception is ArgumentOutOfRangeException or KeyNotFoundException)
        {
            return null;
        }
    }

    private static PageNumberFormat? ToPageNumberFormat(NumberFormatValues? value)
    {
        if (value is null)
        {
            return null;
        }

        var format = value.Value;

        if (format == NumberFormatValues.Decimal)
        {
            return PageNumberFormat.DecimalNumber;
        }

        if (format == NumberFormatValues.UpperRoman)
        {
            return PageNumberFormat.UpperRoman;
        }

        if (format == NumberFormatValues.LowerRoman)
        {
            return PageNumberFormat.LowerRoman;
        }

        if (format == NumberFormatValues.UpperLetter)
        {
            return PageNumberFormat.UpperAlphabetic;
        }

        if (format == NumberFormatValues.LowerLetter)
        {
            return PageNumberFormat.LowerAlphabetic;
        }

        return null;
    }

    private static PageSettings ToPageSettings(SectionProperties? properties)
    {
        var size = properties?.GetFirstChild<PageSize>();
        var margin = properties?.GetFirstChild<PageMargin>();
        var widthValue = size?.Width?.Value;
        var heightValue = size?.Height?.Value;
        var width = widthValue is > 0 ? widthValue.Value / 20.0 : PageSettings.Letter.WidthPoints;
        var height = heightValue is > 0
            ? heightValue.Value / 20.0
            : PageSettings.Letter.HeightPoints;
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

    private static SectionProperties ToSectionProperties(
        Section section,
        int sectionIndex,
        MainDocumentPart mainPart,
        Dictionary<Guid, int> numberingIds,
        ref uint nextDrawingId)
    {
        var properties = new SectionProperties();

        if (!section.LinkHeaderToPrevious || sectionIndex == 0)
        {
            AddHeaderFooterReference(
                properties,
                section,
                HeaderFooterKind.Header,
                HeaderFooterVariant.Default,
                mainPart,
                numberingIds,
                ref nextDrawingId);

            if (section.DifferentFirstPage)
            {
                AddHeaderFooterReference(
                    properties,
                    section,
                    HeaderFooterKind.Header,
                    HeaderFooterVariant.FirstPage,
                    mainPart,
                    numberingIds,
                    ref nextDrawingId);
            }

            AddHeaderFooterReference(
                properties,
                section,
                HeaderFooterKind.Footer,
                HeaderFooterVariant.Default,
                mainPart,
                numberingIds,
                ref nextDrawingId);

            if (section.DifferentFirstPage)
            {
                AddHeaderFooterReference(
                    properties,
                    section,
                    HeaderFooterKind.Footer,
                    HeaderFooterVariant.FirstPage,
                    mainPart,
                    numberingIds,
                    ref nextDrawingId);
            }
        }

        var page = section.EffectivePage;

        properties.Append(
            new PageSize
            {
                Width = (uint)Math.Round(page.EffectiveWidthPoints * 20),
                Height = (uint)Math.Round(page.EffectiveHeightPoints * 20),
                Orient =
                    page.Orientation == PageOrientation.Landscape
                        ? PageOrientationValues.Landscape
                        : PageOrientationValues.Portrait,
            });

        properties.Append(
            new PageMargin
            {
                Left = (uint)Math.Round(page.MarginLeftPoints * 20),
                Top = (int)Math.Round(page.MarginTopPoints * 20),
                Right = (uint)Math.Round(page.MarginRightPoints * 20),
                Bottom = (int)Math.Round(page.MarginBottomPoints * 20),
            });

        if (section.PageNumberStart is not null || section.PageNumberFormat is not null)
        {
            var pageNumber = new PageNumberType();

            if (section.PageNumberStart is { } start)
            {
                pageNumber.Start = start;
            }

            if (section.PageNumberFormat is { } format)
            {
                pageNumber.Format = ToWordPageNumberFormat(format);
            }

            properties.Append(pageNumber);
        }

        if (section.DifferentFirstPage)
        {
            properties.Append(new TitlePage());
        }

        return properties;
    }

    private static void AddHeaderFooterReference(
        SectionProperties properties,
        Section section,
        HeaderFooterKind kind,
        HeaderFooterVariant variant,
        MainDocumentPart mainPart,
        Dictionary<Guid, int> numberingIds,
        ref uint nextDrawingId)
    {
        var content = section.GetHeaderFooter(kind, variant);

        if (content is null || !content.HasVisibleContent)
        {
            return;
        }

        var imageRelationships = new Dictionary<string, string>(StringComparer.Ordinal);

        if (kind == HeaderFooterKind.Header)
        {
            var part = mainPart.AddNewPart<HeaderPart>();
            var header = new Header();

            foreach (var block in content.Blocks)
            {
                if (block is ParagraphBlock paragraph)
                {
                    AppendParagraph(
                        header,
                        paragraph,
                        numberingIds,
                        part,
                        imageRelationships,
                        ref nextDrawingId);
                }
            }

            part.Header = header;
            properties.Append(
                new HeaderReference
                {
                    Type = ToWordHeaderFooterValues(variant),
                    Id = mainPart.GetIdOfPart(part),
                });
        }
        else
        {
            var part = mainPart.AddNewPart<FooterPart>();
            var footer = new Footer();

            foreach (var block in content.Blocks)
            {
                if (block is ParagraphBlock paragraph)
                {
                    AppendParagraph(
                        footer,
                        paragraph,
                        numberingIds,
                        part,
                        imageRelationships,
                        ref nextDrawingId);
                }
            }

            part.Footer = footer;
            properties.Append(
                new FooterReference
                {
                    Type = ToWordHeaderFooterValues(variant),
                    Id = mainPart.GetIdOfPart(part),
                });
        }
    }

    private static HeaderFooterValues ToWordHeaderFooterValues(
        HeaderFooterVariant variant) =>
        variant == HeaderFooterVariant.FirstPage
            ? HeaderFooterValues.First
            : HeaderFooterValues.Default;

    private static NumberFormatValues ToWordPageNumberFormat(
        PageNumberFormat format) =>
        format switch
        {
            PageNumberFormat.UpperRoman => NumberFormatValues.UpperRoman,
            PageNumberFormat.LowerRoman => NumberFormatValues.LowerRoman,
            PageNumberFormat.UpperAlphabetic => NumberFormatValues.UpperLetter,
            PageNumberFormat.LowerAlphabetic => NumberFormatValues.LowerLetter,
            _ => NumberFormatValues.Decimal,
        };

    private static CoreRunProperties? ToRunProperties(WpRunProperties? properties) =>
        properties is null
            ? null
            : ParseRunProperties(properties, properties.GetFirstChild<RunStyle>()?.Val?.Value);

    private static CoreRunProperties? ParseRunProperties(OpenXmlElement properties, string? styleId)
    {
        var fonts = properties.GetFirstChild<RunFonts>();
        var family = fonts?.Ascii?.Value
            ?? fonts?.HighAnsi?.Value
            ?? fonts?.ComplexScript?.Value
            ?? fonts?.EastAsia?.Value;
        var halfPoints = ParseHalfPoints(properties.GetFirstChild<FontSize>()?.Val?.Value)
            ?? ParseHalfPoints(properties.GetFirstChild<FontSizeComplexScript>()?.Val?.Value);
        var boldElement = properties.GetFirstChild<Bold>();
        var italicElement = properties.GetFirstChild<Italic>();
        var underlineElement = properties.GetFirstChild<Underline>();
        var strikeElement = properties.GetFirstChild<Strike>();
        var colorElement = properties.GetFirstChild<Color>();
        var highlightElement = properties.GetFirstChild<Highlight>();
        var shadingElement = properties.GetFirstChild<Shading>();
        var bold = boldElement is not null && (boldElement.Val?.Value ?? true);
        var italic = italicElement is not null && (italicElement.Val?.Value ?? true);
        var underline =
            underlineElement is not null && underlineElement.Val?.Value != UnderlineValues.None;
        var strikethrough = strikeElement is not null && (strikeElement.Val?.Value ?? true);
        var color = NormalizeHex(colorElement?.Val?.Value);
        var highlight = HighlightToHex(highlightElement?.Val?.InnerText)
            ?? NormalizeHex(shadingElement?.Fill?.Value);
        var overrides = RunPropertyOverrides.None;
        if (family is not null)
        {
            overrides |= RunPropertyOverrides.FontFamily;
        }
        if (halfPoints is not null)
        {
            overrides |= RunPropertyOverrides.FontSize;
        }
        if (boldElement is not null)
        {
            overrides |= RunPropertyOverrides.Bold;
        }
        if (italicElement is not null)
        {
            overrides |= RunPropertyOverrides.Italic;
        }
        if (underlineElement is not null)
        {
            overrides |= RunPropertyOverrides.Underline;
        }
        if (strikeElement is not null)
        {
            overrides |= RunPropertyOverrides.Strikethrough;
        }
        if (colorElement is not null)
        {
            overrides |= RunPropertyOverrides.Color;
        }
        if (highlightElement is not null || shadingElement is not null)
        {
            overrides |= RunPropertyOverrides.Highlight;
        }
        if (string.IsNullOrWhiteSpace(styleId) && overrides == RunPropertyOverrides.None)
        {
            return null;
        }
        return new CoreRunProperties
        {
            FontFamily = string.IsNullOrWhiteSpace(family) ? "Arial" : family,
            FontSizePoints = halfPoints is > 0 and <= 800 ? halfPoints.Value / 2 : 11,
            Bold = bold,
            Italic = italic,
            Underline = underline,
            Strikethrough = strikethrough,
            ColorHex = color is null ? "#000000" : "#" + color,
            HighlightHex = highlight is null ? null : "#" + highlight,
            StyleId = styleId,
            Overrides = overrides,
        };
    }

    private static WpRunProperties? ToWordRunProperties(CoreRunProperties? properties)
    {
        if (properties is null)
        {
            return null;
        }
        var hasStyle = !string.IsNullOrWhiteSpace(properties.StyleId);
        var runProperties = new WpRunProperties();
        bool Writes(RunPropertyOverrides flag) => !hasStyle || properties.Overrides.HasFlag(flag);
        if (hasStyle)
        {
            runProperties.Append(new RunStyle { Val = properties.StyleId });
        }
        if (Writes(RunPropertyOverrides.FontFamily))
        {
            runProperties.Append(
                new RunFonts { Ascii = properties.FontFamily, HighAnsi = properties.FontFamily }
            );
        }
        if (hasStyle && Writes(RunPropertyOverrides.Bold))
        {
            runProperties.Append(new Bold { Val = properties.Bold });
        }
        else if (properties.Bold)
        {
            runProperties.Append(new Bold());
        }
        if (hasStyle && Writes(RunPropertyOverrides.Italic))
        {
            runProperties.Append(new Italic { Val = properties.Italic });
        }
        else if (properties.Italic)
        {
            runProperties.Append(new Italic());
        }
        if (hasStyle && Writes(RunPropertyOverrides.Strikethrough))
        {
            runProperties.Append(new Strike { Val = properties.Strikethrough });
        }
        else if (properties.Strikethrough)
        {
            runProperties.Append(new Strike());
        }
        var color = NormalizeHex(properties.ColorHex);
        var writeColor = hasStyle ? Writes(RunPropertyOverrides.Color) : color is not null;
        if (writeColor)
        {
            runProperties.Append(new Color { Val = color ?? "000000" });
        }
        if (Writes(RunPropertyOverrides.FontSize))
        {
            runProperties.Append(
                new FontSize
                {
                    Val = ((int)Math.Round(properties.FontSizePoints * 2)).ToString(
                        CultureInfo.InvariantCulture
                    ),
                }
            );
        }
        if (hasStyle && Writes(RunPropertyOverrides.Underline))
        {
            runProperties.Append(
                new Underline
                {
                    Val = properties.Underline ? UnderlineValues.Single : UnderlineValues.None,
                }
            );
        }
        else if (properties.Underline)
        {
            runProperties.Append(new Underline { Val = UnderlineValues.Single });
        }
        var highlight = NormalizeHex(properties.HighlightHex);
        var writeHighlight = hasStyle
            ? Writes(RunPropertyOverrides.Highlight)
            : highlight is not null;
        if (writeHighlight)
        {
            runProperties.Append(new Shading { Fill = highlight ?? "auto" });
        }
        return runProperties.ChildElements.Count > 0 ? runProperties : null;
    }

    private static double? ParseHalfPoints(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static string? NormalizeHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var trimmed = value.Trim().TrimStart('#').ToUpperInvariant();
        return trimmed.Length == 6 && trimmed.All(character => Uri.IsHexDigit(character))
            ? trimmed
            : null;
    }

    private static string? HighlightToHex(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "yellow" => "#FFFF00",
            "green" => "#00FF00",
            "cyan" => "#00FFFF",
            "magenta" => "#FF00FF",
            "blue" => "#0000FF",
            "red" => "#FF0000",
            "darkblue" => "#000080",
            "darkcyan" => "#008080",
            "darkgreen" => "#008000",
            "darkmagenta" => "#800080",
            "darkred" => "#800000",
            "darkyellow" => "#808000",
            "gray" => "#808080",
            "lightgray" => "#C0C0C0",
            "black" => "#000000",
            _ => null,
        };

    private static StyleSheet LoadStyles(MainDocumentPart mainPart, Action<string, string> report)
    {
        var definitions = mainPart.StyleDefinitionsPart?.Styles;
        if (definitions is null)
        {
            return StyleSheet.Default;
        }
        var paragraphStyles = StyleSheet.Default.ParagraphStyles.ToList();
        var characterStyles = StyleSheet.Default.CharacterStyles.ToList();
        try
        {
            foreach (var style in definitions.Elements<WpStyle>())
            {
                var id = style.StyleId?.Value;
                if (string.IsNullOrWhiteSpace(id))
                {
                    report(
                        DocxDiagnosticCodes.UnsupportedStyle,
                        "A style definition without an ID was ignored."
                    );
                    continue;
                }
                var name = style.StyleName?.Val?.Value?.Trim();
                name = string.IsNullOrWhiteSpace(name) ? id : name;
                if (style.Type?.Value == StyleValues.Paragraph)
                {
                    var isDefault = style.Default?.Value == true;
                    var targetId = isDefault ? StyleSheet.NormalStyleId : id;
                    var targetName = isDefault ? "Normal" : name;
                    var properties = ParseStyleParagraphProperties(style.StyleParagraphProperties)
                        ?? CoreParagraphProperties.Default;
                    var characterProperties = ParseStyleRunProperties(style.StyleRunProperties)
                        ?? CoreRunProperties.Default;
                    Upsert(
                        paragraphStyles,
                        new ParagraphStyle(targetId, targetName, properties, characterProperties),
                        candidate => candidate.Id
                    );
                }
                else if (style.Type?.Value == StyleValues.Character)
                {
                    var isDefault = style.Default?.Value == true;
                    var targetId = isDefault ? StyleSheet.DefaultCharacterStyleId : id;
                    var targetName = isDefault ? "Default" : name;
                    var properties = ParseStyleRunProperties(style.StyleRunProperties)
                        ?? CoreRunProperties.Default;
                    Upsert(
                        characterStyles,
                        new CharacterStyle(targetId, targetName, properties),
                        candidate => candidate.Id
                    );
                }
            }
        }
        catch (Exception exception)
            when (exception is InvalidDataException or InvalidOperationException or FormatException)
        {
            report(
                DocxDiagnosticCodes.UnsupportedStyle,
                "Some style definitions could not be read."
            );
        }
        return new StyleSheet(
            paragraphStyles.ToImmutableArray(),
            characterStyles.ToImmutableArray()
        );
    }

    private static CoreParagraphProperties? ParseStyleParagraphProperties(
        StyleParagraphProperties? properties
    )
    {
        if (properties is null)
        {
            return null;
        }
        var justification = properties.GetFirstChild<Justification>();
        var spacing = properties.GetFirstChild<SpacingBetweenLines>();
        var indentation = properties.GetFirstChild<Indentation>();
        var alignment = justification?.Val?.InnerText?.ToLowerInvariant() switch
        {
            "center" => ParagraphAlignment.Center,
            "right" or "end" => ParagraphAlignment.Right,
            "both" => ParagraphAlignment.Justify,
            _ => ParagraphAlignment.Left,
        };
        var firstLinePoints = ParseTwips(indentation?.FirstLine?.Value)
            ?? (ParseTwips(indentation?.Hanging?.Value) is { } hanging ? -hanging : 0);
        if (justification is null && spacing is null && indentation is null)
        {
            return null;
        }
        return new CoreParagraphProperties
        {
            Alignment = alignment,
            SpaceBeforePoints = ParseTwips(spacing?.Before?.Value) ?? 0,
            SpaceAfterPoints = ParseTwips(spacing?.After?.Value) ?? 8,
            LineSpacing = ParseLineSpacing(spacing),
            LeftIndentPoints = ParseTwips(indentation?.Left?.Value) ?? 0,
            RightIndentPoints = ParseTwips(indentation?.Right?.Value) ?? 0,
            FirstLineIndentPoints = firstLinePoints,
        };
    }

    private static CoreRunProperties? ParseStyleRunProperties(StyleRunProperties? properties) =>
        properties is null ? null : ParseRunProperties(properties, styleId: null);

    private static void SaveStyles(MainDocumentPart mainPart, StyleSheet styleSheet)
    {
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();
        foreach (var style in styleSheet.ParagraphStyles)
        {
            var element = new WpStyle
            {
                Type = StyleValues.Paragraph,
                StyleId = style.Id,
                Default = style.Id == StyleSheet.NormalStyleId ? true : null,
            };
            element.Append(new StyleName { Val = style.Name });
            var paragraphProperties = ToStyleParagraphProperties(style.Properties);
            var runProperties = ToStyleRunProperties(
                style.CharacterProperties ?? CoreRunProperties.Default
            );
            if (paragraphProperties is not null)
            {
                element.Append(paragraphProperties);
            }
            if (runProperties is not null)
            {
                element.Append(runProperties);
            }
            styles.Append(element);
        }
        foreach (var style in styleSheet.CharacterStyles)
        {
            var element = new WpStyle
            {
                Type = StyleValues.Character,
                StyleId = style.Id,
                Default = style.Id == StyleSheet.DefaultCharacterStyleId ? true : null,
            };
            element.Append(new StyleName { Val = style.Name });
            var runProperties = ToStyleRunProperties(style.Properties);
            if (runProperties is not null)
            {
                element.Append(runProperties);
            }
            styles.Append(element);
        }
        stylesPart.Styles = styles;
    }

    private static StyleParagraphProperties? ToStyleParagraphProperties(
        CoreParagraphProperties properties
    )
    {
        var element = new StyleParagraphProperties();
        var hasContent = false;
        if (
            properties.SpaceBeforePoints != 0
            || properties.SpaceAfterPoints != 8
            || Math.Abs(properties.LineSpacing - 1.15) > 0.0001
        )
        {
            element.Append(
                new SpacingBetweenLines
                {
                    Before = ToTwips(properties.SpaceBeforePoints),
                    After = ToTwips(properties.SpaceAfterPoints),
                    Line = ((int)Math.Round(properties.LineSpacing * 240)).ToString(
                        CultureInfo.InvariantCulture
                    ),
                    LineRule = LineSpacingRuleValues.Auto,
                }
            );
            hasContent = true;
        }
        if (
            properties.LeftIndentPoints != 0
            || properties.RightIndentPoints != 0
            || properties.FirstLineIndentPoints != 0
        )
        {
            var indentation = new Indentation
            {
                Left = ToTwips(properties.LeftIndentPoints),
                Right = ToTwips(properties.RightIndentPoints),
            };
            if (properties.FirstLineIndentPoints >= 0)
            {
                indentation.FirstLine = ToTwips(properties.FirstLineIndentPoints);
            }
            else
            {
                indentation.Hanging = ToTwips(-properties.FirstLineIndentPoints);
            }
            element.Append(indentation);
            hasContent = true;
        }
        if (properties.Alignment != ParagraphAlignment.Left)
        {
            element.Append(
                new Justification
                {
                    Val = properties.Alignment switch
                    {
                        ParagraphAlignment.Center => JustificationValues.Center,
                        ParagraphAlignment.Right => JustificationValues.Right,
                        ParagraphAlignment.Justify => JustificationValues.Both,
                        _ => JustificationValues.Left,
                    },
                }
            );
            hasContent = true;
        }
        return hasContent ? element : null;
    }

    private static StyleRunProperties ToStyleRunProperties(CoreRunProperties properties)
    {
        var element = new StyleRunProperties(
            new RunFonts { Ascii = properties.FontFamily, HighAnsi = properties.FontFamily }
        );
        if (properties.Bold)
        {
            element.Append(new Bold());
        }
        if (properties.Italic)
        {
            element.Append(new Italic());
        }
        if (properties.Strikethrough)
        {
            element.Append(new Strike());
        }
        var color = NormalizeHex(properties.ColorHex);
        if (color is not null)
        {
            element.Append(new Color { Val = color });
        }
        element.Append(
            new FontSize
            {
                Val = ((int)Math.Round(properties.FontSizePoints * 2)).ToString(
                    CultureInfo.InvariantCulture
                ),
            }
        );
        if (properties.Underline)
        {
            element.Append(new Underline { Val = UnderlineValues.Single });
        }
        var highlight = NormalizeHex(properties.HighlightHex);
        if (highlight is not null)
        {
            element.Append(new Shading { Fill = highlight });
        }
        return element;
    }

    private static void Upsert<T>(List<T> styles, T style, Func<T, string> idSelector)
    {
        var id = idSelector(style);
        var index = styles.FindIndex(candidate =>
            string.Equals(idSelector(candidate), id, StringComparison.Ordinal)
        );
        if (index < 0)
        {
            styles.Add(style);
        }
        else
        {
            styles[index] = style;
        }
    }

    private sealed record ImportedImage(
        ImageFormat Format,
        ImmutableArray<byte> Data,
        int PixelWidth,
        int PixelHeight
    );

    private static InlineImage? TryCreateInlineImage(
        WpDrawing drawing,
        OpenXmlPart ownerPart,
        Dictionary<string, ImportedImage> images,
        Action<string, string> report
    )
    {
        if (drawing.GetFirstChild<Dwp.Anchor>() is not null)
        {
            report(
                DocxDiagnosticCodes.UnsupportedImage,
                "Floating images are not supported and were skipped."
            );
            return null;
        }
        var inline = drawing.GetFirstChild<Dwp.Inline>();
        if (inline is null)
        {
            report(
                DocxDiagnosticCodes.UnsupportedImage,
                "An image in this document uses an unsupported drawing format."
            );
            return null;
        }
        var properties = inline.DocProperties;
        var name = string.IsNullOrWhiteSpace(properties?.Name?.Value)
            ? "Image"
            : properties.Name.Value;
        var altText = properties?.Description?.Value ?? string.Empty;
        var width = inline.Extent?.Cx?.Value is { } cx && cx > 0 ? cx / 12700.0 : 0;
        var height = inline.Extent?.Cy?.Value is { } cy && cy > 0 ? cy / 12700.0 : 0;
        var picture = inline.Graphic?.GraphicData?.GetFirstChild<Pic.Picture>();
        var relationshipId = picture?.BlipFill?.Blip?.Embed?.Value;
        if (relationshipId is null)
        {
            report(
                DocxDiagnosticCodes.UnsupportedImage,
                "An image in this document has no embedded media."
            );
            return CreatePlaceholder(width, height, name, altText);
        }
        var cacheKey = $"{ownerPart.Uri}#{relationshipId}";

        if (!images.TryGetValue(cacheKey, out var imported))
        {
            ImagePart? part;
            try
            {
                part = ownerPart.GetPartById(relationshipId) as ImagePart;
            }
            catch (Exception exception)
                when (exception is ArgumentOutOfRangeException or KeyNotFoundException)
            {
                part = null;
            }
            if (part is null)
            {
                report(
                    DocxDiagnosticCodes.UnsupportedImage,
                    "An embedded image is missing and was replaced by a placeholder."
                );
                return CreatePlaceholder(width, height, name, altText);
            }
            using var stream = part.GetStream();
            if (stream.Length > ImageValidation.MaxImageBytes)
            {
                report(
                    DocxDiagnosticCodes.UnsupportedImage,
                    "An embedded image is larger than 50 MB and was replaced by a placeholder."
                );
                return CreatePlaceholder(width, height, name, altText);
            }
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var data = buffer.ToArray();
            var inspected = ImageValidation.Inspect(data);
            if (!inspected.IsSuccess || inspected.Value is null)
            {
                var reason = inspected.Diagnostics.FirstOrDefault()?.Message
                    ?? "An embedded image is damaged.";
                report(DocxDiagnosticCodes.UnsupportedImage, reason);
                return CreatePlaceholder(width, height, name, altText);
            }
            imported = new ImportedImage(
                inspected.Value.Format,
                data.ToImmutableArray(),
                inspected.Value.PixelWidth,
                inspected.Value.PixelHeight
            );
            images[cacheKey] = imported;
        }
        if (width <= 0 || height <= 0)
        {
            var natural = ImageValidation.NaturalSize(
                new ImageMetadata(imported.Format, imported.PixelWidth, imported.PixelHeight)
            );
            width = natural.Width;
            height = natural.Height;
        }
        var locks =
            picture?.NonVisualPictureProperties?.NonVisualPictureDrawingProperties?.GetFirstChild<A.PictureLocks>();
        var lockAspectRatio = locks?.NoChangeAspect?.Value ?? true;
        return new InlineImage(
            imported.Format,
            name,
            imported.Data,
            imported.PixelWidth,
            imported.PixelHeight,
            width,
            height,
            altText,
            lockAspectRatio
        );
    }

    private static InlineImage CreatePlaceholder(
        double width,
        double height,
        string name,
        string altText
    ) =>
        new(
            ImageFormat.Png,
            name,
            ImmutableArray<byte>.Empty,
            0,
            0,
            width > 0 ? width : 200,
            height > 0 ? height : 100,
            altText,
            LockAspectRatio: true,
            IsAvailable: false
        );

    private sealed record ParsedTableCell(CoreTableCell Cell, bool IsRestart, bool IsContinuation);

    private sealed record ParsedGridCell(int StartColumn, int Span, ParsedTableCell Cell);

    private static CoreTableBlock ToTableBlock(
        WpTable table,
        OpenXmlPart ownerPart,
        Dictionary<string, ImportedImage> images,
        Action<string, string> report,
        Numbering? numbering,
        Dictionary<long, Guid> listIds
    )
    {
        if (table.Descendants<WpTable>().Any())
        {
            report(DocxDiagnosticCodes.UnsupportedTable, "Nested tables are not supported.");
        }
        var properties = table.GetFirstChild<TableProperties>();
        var grid = table.GetFirstChild<TableGrid>();
        var gridColumns = grid?.Elements<GridColumn>().ToList() ?? [];
        var rowElements = table.Elements<WpTableRow>().ToList();
        var columnCount = gridColumns.Count;
        if (columnCount == 0)
        {
            columnCount =
                rowElements.Count == 0
                    ? 1
                    : Math.Max(
                        1,
                        rowElements.Max(row =>
                            row.Elements<WpTableCell>()
                                .Sum(cell =>
                                    Math.Max(
                                        1,
                                        (int)(
                                            cell.GetFirstChild<TableCellProperties>()?.GridSpan?.Val?.Value
                                            ?? 1
                                        )
                                    )
                                )
                        )
                    );
        }
        var widths = new double[columnCount];
        for (var index = 0; index < columnCount && index < gridColumns.Count; index++)
        {
            widths[index] = ParseTwips(gridColumns[index].Width?.Value) ?? 0;
        }
        if (widths.Sum() <= 0)
        {
            Array.Fill(widths, 468d / columnCount);
        }
        else if (widths.Any(width => width <= 0))
        {
            var average = widths.Where(width => width > 0).Average();
            for (var index = 0; index < widths.Length; index++)
            {
                if (widths[index] <= 0)
                {
                    widths[index] = average;
                }
            }
        }
        var borders = ToCoreTableBorders(properties?.TableBorders) ?? CoreTableBorders.Default;
        var parsedRows = new List<(bool IsHeader, List<ParsedTableCell> Cells)>();
        foreach (var rowElement in rowElements)
        {
            var isHeader =
                rowElement.GetFirstChild<TableRowProperties>()?.GetFirstChild<TableHeader>()
                is not null;
            var parsedCells = new List<ParsedTableCell>();
            foreach (var cellElement in rowElement.Elements<WpTableCell>())
            {
                var cellProperties = cellElement.GetFirstChild<TableCellProperties>();
                var span = Math.Max(1, (int)(cellProperties?.GridSpan?.Val?.Value ?? 1));
                var verticalMerge = cellProperties?.VerticalMerge?.Val?.Value;
                var shading = NormalizeHex(cellProperties?.Shading?.Fill?.Value) is { } fill
                    ? "#" + fill
                    : null;
                var cellBorders = ToCoreTableBorders(cellProperties?.TableCellBorders) ?? borders;
                var paragraphs = cellElement
                    .Elements<WpParagraph>()
                    .Select(paragraph =>
                        ToParagraphBlock(paragraph, ownerPart, images, report, numbering, listIds)
                    )
                    .ToImmutableArray();
                if (paragraphs.Length == 0)
                {
                    paragraphs = ImmutableArray.Create(ParagraphBlock.Create());
                }
                if (cellElement.Descendants<WpTable>().Any())
                {
                    report(
                        DocxDiagnosticCodes.UnsupportedTable,
                        "Nested tables are not supported."
                    );
                }
                var cell = new CoreTableCell(
                    Guid.NewGuid(),
                    paragraphs,
                    ColumnSpan: span,
                    ShadingHex: shading,
                    Borders: cellBorders,
                    IsMergedContinuation: verticalMerge == MergedCellValues.Continue
                );
                parsedCells.Add(
                    new ParsedTableCell(
                        cell,
                        verticalMerge == MergedCellValues.Restart,
                        verticalMerge == MergedCellValues.Continue
                    )
                );
            }
            parsedRows.Add((isHeader, parsedCells));
        }
        var origins = new List<List<ParsedGridCell>>();
        var maximumColumn = 0;
        foreach (var (_, rowCells) in parsedRows)
        {
            var rowOrigins = new List<ParsedGridCell>();
            var gridColumn = 0;
            foreach (var parsedCell in rowCells)
            {
                rowOrigins.Add(
                    new ParsedGridCell(gridColumn, parsedCell.Cell.ColumnSpan, parsedCell)
                );
                gridColumn += Math.Max(1, parsedCell.Cell.ColumnSpan);
            }
            origins.Add(rowOrigins);
            maximumColumn = Math.Max(maximumColumn, gridColumn);
        }
        if (maximumColumn > columnCount)
        {
            var extended = new double[maximumColumn];
            Array.Copy(widths, extended, widths.Length);
            var average = widths.Length == 0 ? 90 : widths.Average();
            for (var index = widths.Length; index < extended.Length; index++)
            {
                extended[index] = average;
            }
            widths = extended;
            columnCount = maximumColumn;
        }
        var tableRows = parsedRows
            .Select(
                (row, _) =>
                    new CoreTableRow(
                        Guid.NewGuid(),
                        row.Cells.Select(cell => cell.Cell).ToImmutableArray(),
                        row.IsHeader
                    )
            )
            .ToList();
        for (var rowIndex = 0; rowIndex < parsedRows.Count; rowIndex++)
        {
            for (var cellIndex = 0; cellIndex < parsedRows[rowIndex].Cells.Count; cellIndex++)
            {
                var parsed = parsedRows[rowIndex].Cells[cellIndex];
                if (!parsed.IsRestart)
                {
                    continue;
                }
                var origin = origins[rowIndex]
                    .FirstOrDefault(item => item.Cell.Cell.Id == parsed.Cell.Id);
                if (origin is null)
                {
                    continue;
                }
                var rowSpan = 1;
                for (var nextRow = rowIndex + 1; nextRow < origins.Count; nextRow++)
                {
                    var covering = origins[nextRow]
                        .FirstOrDefault(item =>
                            item.StartColumn <= origin.StartColumn
                            && origin.StartColumn < item.StartColumn + item.Span
                        );
                    if (covering is null || !covering.Cell.IsContinuation)
                    {
                        break;
                    }
                    rowSpan++;
                }
                if (rowSpan > 1)
                {
                    tableRows[rowIndex] = tableRows[rowIndex] with
                    {
                        Cells = tableRows[rowIndex]
                            .Cells.SetItem(cellIndex, parsed.Cell with { RowSpan = rowSpan }),
                    };
                }
            }
        }
        var tableWidth = ParseTwips(properties?.TableWidth?.Width?.Value) ?? widths.Sum();
        return new CoreTableBlock(
            Guid.NewGuid(),
            tableRows.ToImmutableArray(),
            widths.ToImmutableArray(),
            parsedRows.Any(row => row.IsHeader),
            tableWidth,
            borders
        );
    }

    private static CoreTableBorders? ToCoreTableBorders(OpenXmlElement? borders)
    {
        if (borders is null)
        {
            return null;
        }
        var top = borders.GetFirstChild<TopBorder>();
        var bottom = borders.GetFirstChild<BottomBorder>();
        var left = borders.GetFirstChild<LeftBorder>();
        var right = borders.GetFirstChild<RightBorder>();
        if (top is null && bottom is null && left is null && right is null)
        {
            return null;
        }
        var color = NormalizeHex(
            top?.Color?.Value ?? left?.Color?.Value ?? bottom?.Color?.Value ?? right?.Color?.Value
        ) ?? "#000000";
        var size = ParseEighthPoints(top?.Size?.Value) ?? 0.5;
        return new CoreTableBorders(
            left is not null && left.Val?.Value != BorderValues.Nil,
            top is not null && top.Val?.Value != BorderValues.Nil,
            right is not null && right.Val?.Value != BorderValues.Nil,
            bottom is not null && bottom.Val?.Value != BorderValues.Nil,
            color,
            size
        );
    }

    private static double? ParseEighthPoints(uint? value) =>
        value is null ? null : value.Value / 8.0;

    private static ParagraphBlock ToParagraphBlock(
        WpParagraph paragraph,
        OpenXmlPart ownerPart,
        Dictionary<string, ImportedImage> images,
        Action<string, string> report,
        Numbering? numbering,
        Dictionary<long, Guid> listIds
    )
    {
        if (paragraph.Descendants<Hyperlink>().Any())
        {
            report(
                DocxDiagnosticCodes.UnsupportedHyperlink,
                "Hyperlink styling is not shown in this preview."
            );
        }
        if (paragraph.Descendants<Picture>().Any())
        {
            report(
                DocxDiagnosticCodes.UnsupportedImage,
                "Legacy or vector images are not supported and were skipped."
            );
        }
        if (paragraph.Descendants<InsertedRun>().Any() || paragraph.Descendants<DeletedRun>().Any())
        {
            report(
                DocxDiagnosticCodes.UnsupportedTrackedChange,
                "Tracked changes are not shown in this preview."
            );
        }

        var simpleFieldFirstRuns = new Dictionary<WpRun, PageFieldKind>();
        var simpleFieldRuns = new HashSet<WpRun>();
        var unsupportedFieldReported = false;

        void ReportUnsupportedField()
        {
            if (!unsupportedFieldReported)
            {
                unsupportedFieldReported = true;
                report(
                    DocxDiagnosticCodes.UnsupportedField,
                    "Only PAGE and NUMPAGES fields are evaluated in headers and footers."
                );
            }
        }

        foreach (var field in paragraph.Descendants<SimpleField>())
        {
            var kind = ParsePageFieldInstruction(field.Instruction?.Value);

            if (kind is null)
            {
                ReportUnsupportedField();
                continue;
            }

            var firstRun = field.Descendants<WpRun>().FirstOrDefault();

            if (firstRun is not null)
            {
                simpleFieldFirstRuns[firstRun] = kind.Value;
            }

            foreach (var fieldRun in field.Descendants<WpRun>())
            {
                simpleFieldRuns.Add(fieldRun);
            }
        }

        var runs = ImmutableArray.CreateBuilder<CoreRun>();
        var inField = false;
        var inFieldResult = false;
        var fieldEmitted = false;
        PageFieldKind? pendingField = null;

        foreach (var run in paragraph.Descendants<WpRun>())
        {
            var properties = ToRunProperties(run.GetFirstChild<WpRunProperties>());

            if (simpleFieldFirstRuns.TryGetValue(run, out var simpleFieldKind))
            {
                runs.Add(CoreRun.CreatePageField(simpleFieldKind, properties));
                continue;
            }

            if (simpleFieldRuns.Contains(run))
            {
                continue;
            }

            var fieldChar = run.GetFirstChild<FieldChar>();

            if (fieldChar is not null)
            {
                var fieldType = fieldChar.FieldCharType?.Value;

                if (fieldType == FieldCharValues.Begin)
                {
                    inField = true;
                    inFieldResult = false;
                    fieldEmitted = false;
                    pendingField = null;
                }
                else if (fieldType == FieldCharValues.Separate)
                {
                    inFieldResult = true;

                    if (pendingField is { } separatedKind && !fieldEmitted)
                    {
                        runs.Add(CoreRun.CreatePageField(separatedKind, properties));
                        fieldEmitted = true;
                    }
                }
                else if (fieldType == FieldCharValues.End)
                {
                    if (inField
                        && pendingField is { } endedKind
                        && !fieldEmitted)
                    {
                        runs.Add(CoreRun.CreatePageField(endedKind, properties));
                    }

                    inField = false;
                    inFieldResult = false;
                    fieldEmitted = false;
                    pendingField = null;
                }

                continue;
            }

            if (inField)
            {
                if (!inFieldResult)
                {
                    var instruction = run.GetFirstChild<FieldCode>()?.Text;
                    var kind = ParsePageFieldInstruction(instruction);

                    if (kind is null)
                    {
                        ReportUnsupportedField();
                    }
                    else
                    {
                        pendingField = kind;
                    }
                }

                continue;
            }

            var text = new StringBuilder();
            void FlushText()
            {
                if (text.Length == 0)
                {
                    return;
                }
                runs.Add(new CoreRun(Guid.NewGuid(), text.ToString(), properties));
                text.Clear();
            }
            foreach (var child in run.ChildElements)
            {
                switch (child)
                {
                    case WpText runText:
                        text.Append(runText.Text);
                        break;
                    case Break lineBreak:
                        if (
                            lineBreak.Type is null
                            || lineBreak.Type.Value == BreakValues.TextWrapping
                        )
                        {
                            text.Append(TextBreaks.SoftBreak);
                        }
                        else if (lineBreak.Type.Value == BreakValues.Page)
                        {
                            text.Append(TextBreaks.PageBreak);
                        }
                        else
                        {
                            report(
                                DocxDiagnosticCodes.UnsupportedRunContent,
                                "Column breaks are not shown in this preview."
                            );
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
                    case WpRunProperties:
                        break;
                    case WpDrawing drawing:
                        FlushText();
                        var image = TryCreateInlineImage(drawing, ownerPart, images, report);
                        if (image is not null)
                        {
                            runs.Add(CoreRun.CreateImage(image, properties));
                        }
                        break;
                    default:
                        report(
                            DocxDiagnosticCodes.UnsupportedRunContent,
                            "Some run content is not shown in this preview."
                        );
                        break;
                }
            }
            FlushText();
        }
        return new ParagraphBlock(
            Guid.NewGuid(),
            runs.ToImmutable(),
            ToParagraphProperties(paragraph.ParagraphProperties, numbering, listIds)
        );
    }

    private static PageFieldKind? ParsePageFieldInstruction(string? instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
        {
            return null;
        }

        var token = instruction
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()
            ?.ToUpperInvariant();

        return token switch
        {
            "PAGE" => PageFieldKind.PageNumber,
            "NUMPAGES" => PageFieldKind.TotalPages,
            _ => null,
        };
    }

    private static CoreParagraphProperties? ToParagraphProperties(
        WpParagraphProperties? properties,
        Numbering? numbering,
        Dictionary<long, Guid> listIds
    )
    {
        if (properties is null)
        {
            return null;
        }
        var alignment = properties.Justification?.Val?.InnerText?.ToLowerInvariant() switch
        {
            "center" => ParagraphAlignment.Center,
            "right" or "end" => ParagraphAlignment.Right,
            "both" => ParagraphAlignment.Justify,
            _ => ParagraphAlignment.Left,
        };
        var spacing = properties.SpacingBetweenLines;
        var indentation = properties.Indentation;
        var numberingProperties = properties.NumberingProperties;
        var listLevel = (int?)numberingProperties?.NumberingLevelReference?.Val?.Value + 1 ?? 1;
        var numId = numberingProperties?.NumberingId?.Val?.Value;
        var listKind = ResolveListKind(numbering, numId, listLevel - 1);
        Guid? listId = null;
        if (listKind != ListKind.None && numId is not null)
        {
            if (!listIds.TryGetValue(numId.Value, out var id))
            {
                id = Guid.NewGuid();
                listIds[numId.Value] = id;
            }
            listId = id;
        }
        var firstLinePoints = ParseTwips(indentation?.FirstLine?.Value)
            ?? (ParseTwips(indentation?.Hanging?.Value) is { } hanging ? -hanging : 0);
        var overrides = ParagraphPropertyOverrides.None;
        if (properties.Justification is not null)
        {
            overrides |= ParagraphPropertyOverrides.Alignment;
        }
        if (spacing?.Before is not null)
        {
            overrides |= ParagraphPropertyOverrides.SpaceBefore;
        }
        if (spacing?.After is not null)
        {
            overrides |= ParagraphPropertyOverrides.SpaceAfter;
        }
        if (spacing?.Line is not null)
        {
            overrides |= ParagraphPropertyOverrides.LineSpacing;
        }
        if (indentation?.Left is not null)
        {
            overrides |= ParagraphPropertyOverrides.LeftIndent;
        }
        if (indentation?.Right is not null)
        {
            overrides |= ParagraphPropertyOverrides.RightIndent;
        }
        if (indentation?.FirstLine is not null || indentation?.Hanging is not null)
        {
            overrides |= ParagraphPropertyOverrides.FirstLineIndent;
        }
        if (numberingProperties?.NumberingId is not null)
        {
            overrides |= ParagraphPropertyOverrides.List;
        }
        if (
            alignment == ParagraphAlignment.Left
            && spacing is null
            && indentation is null
            && listKind == ListKind.None
            && properties.ParagraphStyleId?.Val is null
        )
        {
            return null;
        }
        return new CoreParagraphProperties
        {
            Alignment = alignment,
            SpaceBeforePoints = ParseTwips(spacing?.Before?.Value) ?? 0,
            SpaceAfterPoints = ParseTwips(spacing?.After?.Value) ?? 8,
            LineSpacing = ParseLineSpacing(spacing),
            LeftIndentPoints = ParseTwips(indentation?.Left?.Value) ?? 0,
            RightIndentPoints = ParseTwips(indentation?.Right?.Value) ?? 0,
            FirstLineIndentPoints = firstLinePoints,
            ListKind = listKind,
            ListLevel = Math.Clamp(listLevel, 1, 9),
            ListId = listId,
            StyleId = properties.ParagraphStyleId?.Val?.Value,
            Overrides = overrides,
        };
    }

    private static ListKind ResolveListKind(Numbering? numbering, long? numId, int level)
    {
        if (numId is null || numbering is null)
        {
            return ListKind.None;
        }
        var instance = numbering
            .Elements<NumberingInstance>()
            .FirstOrDefault(candidate => candidate.NumberID?.Value == numId.Value);
        var abstractId = instance?.AbstractNumId?.Val?.Value;
        var abstractNumbering = abstractId is null
            ? null
            : numbering
                .Elements<AbstractNum>()
                .FirstOrDefault(candidate => candidate.AbstractNumberId?.Value == abstractId.Value);
        var numberingLevel = abstractNumbering
            ?.Elements<Level>()
            .FirstOrDefault(candidate => candidate.LevelIndex?.Value == level);
        var format = numberingLevel?.NumberingFormat?.Val?.Value;
        return format == NumberFormatValues.Bullet ? ListKind.Bulleted
            : format is null ? ListKind.None
            : ListKind.Numbered;
    }

    private static double? ParseTwips(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed / 20.0
            : null;

    private static double ParseLineSpacing(SpacingBetweenLines? spacing)
    {
        if (
            !double.TryParse(
                spacing?.Line?.Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var line
            )
            || line <= 0
        )
        {
            return 1.15;
        }
        return Math.Clamp(line / 240.0, 0.5, 3.0);
    }
}
