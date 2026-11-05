using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using CoreDocument = MicroslopWords.Core.Documents.Document;
using CoreParagraphProperties = MicroslopWords.Core.Documents.ParagraphProperties;
using CoreRun = MicroslopWords.Core.Documents.Run;
using CoreRunProperties = MicroslopWords.Core.Documents.RunProperties;
using WpDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using WpParagraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using WpParagraphProperties = DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties;
using WpRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using WpRunProperties = DocumentFormat.OpenXml.Wordprocessing.RunProperties;
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
            var numbering = mainPart.NumberingDefinitionsPart?.Numbering;
            var listIds = new Dictionary<long, Guid>();
            SectionProperties? sectionProperties = null;

            foreach (var element in body.ChildElements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                switch (element)
                {
                    case WpParagraph paragraph:
                        blocks.Add(ToParagraphBlock(paragraph, Report, numbering, listIds));
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

        var numberingIds = PrepareNumbering(mainPart, document);

        foreach (var paragraphBlock in document.Sections
                     .SelectMany(section => section.Blocks)
                     .OfType<ParagraphBlock>())
        {
            var paragraph = new WpParagraph();
            var wordParagraphProperties = ToWordParagraphProperties(
                paragraphBlock.EffectiveProperties,
                paragraphBlock.Id,
                numberingIds);

            if (wordParagraphProperties is not null)
            {
                paragraph.Append(wordParagraphProperties);
            }

            foreach (var run in paragraphBlock.Runs)
            {
                var wpRun = new WpRun();
                var wordProperties = ToWordRunProperties(run.Properties);

                if (wordProperties is not null)
                {
                    wpRun.Append(wordProperties);
                }

                AppendRunText(wpRun, run.Text);
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

    private static Dictionary<Guid, int> PrepareNumbering(
        MainDocumentPart mainPart,
        CoreDocument document)
    {
        var ids = new Dictionary<Guid, int>();
        var levelKinds = new Dictionary<Guid, ListKind?[]>();
        var nextId = 1;

        foreach (var paragraph in document.Sections
                     .SelectMany(section => section.Blocks)
                     .OfType<ParagraphBlock>())
        {
            var properties = paragraph.EffectiveProperties;

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
                abstractNum.Append(CreateNumberingLevel(
                    level,
                    kinds[level - 1] ?? ListKind.Numbered));
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
        numberingLevel.Append(new NumberingFormat
        {
            Val = kind == ListKind.Bulleted
                ? NumberFormatValues.Bullet
                : NumberFormatValues.Decimal,
        });
        numberingLevel.Append(new LevelText
        {
            Val = kind == ListKind.Bulleted
                ? BulletForLevel(level)
                : $"%{level}.",
        });
        numberingLevel.Append(new LevelJustification
        {
            Val = LevelJustificationValues.Left,
        });
        numberingLevel.Append(new PreviousParagraphProperties(
            new Indentation
            {
                Left = (level * 360).ToString(CultureInfo.InvariantCulture),
                Hanging = "360",
            }));

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
                run.Append(new WpText(chunk.ToString())
                {
                    Space = SpaceProcessingModeValues.Preserve,
                });
                chunk.Clear();
            }

            run.Append(character == TextBreaks.PageBreak
                ? new Break { Type = BreakValues.Page }
                : new Break());
            wroteBreak = true;
        }

        if (chunk.Length > 0 || !wroteBreak)
        {
            run.Append(new WpText(chunk.ToString())
            {
                Space = SpaceProcessingModeValues.Preserve,
            });
        }
    }

    private static WpParagraphProperties? ToWordParagraphProperties(
        CoreParagraphProperties properties,
        Guid paragraphId,
        Dictionary<Guid, int> numberingIds)
    {
        var wordProperties = new WpParagraphProperties();
        var hasContent = false;

        if (properties.Alignment != ParagraphAlignment.Left)
        {
            wordProperties.Append(new Justification
            {
                Val = properties.Alignment switch
                {
                    ParagraphAlignment.Center => JustificationValues.Center,
                    ParagraphAlignment.Right => JustificationValues.Right,
                    ParagraphAlignment.Justify => JustificationValues.Both,
                    _ => JustificationValues.Left,
                },
            });
            hasContent = true;
        }

        if (properties.SpaceBeforePoints != 0
            || properties.SpaceAfterPoints != 8
            || Math.Abs(properties.LineSpacing - 1.15) > 0.0001)
        {
            wordProperties.Append(new SpacingBetweenLines
            {
                Before = ToTwips(properties.SpaceBeforePoints),
                After = ToTwips(properties.SpaceAfterPoints),
                Line = ((int)Math.Round(properties.LineSpacing * 240))
                    .ToString(CultureInfo.InvariantCulture),
                LineRule = LineSpacingRuleValues.Auto,
            });
            hasContent = true;
        }

        if (properties.LeftIndentPoints != 0
            || properties.RightIndentPoints != 0
            || properties.FirstLineIndentPoints != 0)
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

            wordProperties.Append(indentation);
            hasContent = true;
        }

        if (properties.ListKind != ListKind.None)
        {
            var key = properties.ListId ?? paragraphId;

            if (numberingIds.TryGetValue(key, out var numberId))
            {
                var numbering = new NumberingProperties(
                    new NumberingLevelReference
                    {
                        Val = Math.Clamp(properties.ListLevel, 1, 9) - 1,
                    },
                    new NumberingId { Val = numberId });
                wordProperties.Append(numbering);
                hasContent = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(properties.StyleId))
        {
            wordProperties.Append(new ParagraphStyleId
            {
                Val = properties.StyleId,
            });
            hasContent = true;
        }

        return hasContent ? wordProperties : null;
    }

    private static string ToTwips(double points) =>
        ((int)Math.Round(points * 20)).ToString(CultureInfo.InvariantCulture);
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

    private static CoreRunProperties? ToRunProperties(WpRunProperties? properties)
    {
        if (properties is null)
        {
            return null;
        }

        var fonts = properties.RunFonts;
        var family = fonts?.Ascii?.Value
            ?? fonts?.HighAnsi?.Value
            ?? fonts?.ComplexScript?.Value
            ?? fonts?.EastAsia?.Value;
        var halfPoints = ParseHalfPoints(properties.FontSize?.Val?.Value)
            ?? ParseHalfPoints(properties.FontSizeComplexScript?.Val?.Value);
        var bold = properties.Bold is not null
            && (properties.Bold.Val?.Value ?? true);
        var italic = properties.Italic is not null
            && (properties.Italic.Val?.Value ?? true);
        var underline = properties.Underline is not null
            && properties.Underline.Val?.Value != UnderlineValues.None;
        var strikethrough = properties.Strike is not null
            && (properties.Strike.Val?.Value ?? true);
        var color = NormalizeHex(properties.Color?.Val?.Value);
        var highlight = HighlightToHex(properties.Highlight?.Val?.InnerText)
            ?? NormalizeHex(properties.Shading?.Fill?.Value);

        if (family is null
            && halfPoints is null
            && !bold
            && !italic
            && !underline
            && !strikethrough
            && color is null
            && highlight is null)
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
        };
    }

    private static WpRunProperties? ToWordRunProperties(CoreRunProperties? properties)
    {
        if (properties is null)
        {
            return null;
        }

        var runProperties = new WpRunProperties(
            new RunFonts
            {
                Ascii = properties.FontFamily,
                HighAnsi = properties.FontFamily,
            },
            new FontSize
            {
                Val = ((int)Math.Round(properties.FontSizePoints * 2))
                    .ToString(CultureInfo.InvariantCulture),
            });

        if (properties.Bold)
        {
            runProperties.Append(new Bold());
        }

        if (properties.Italic)
        {
            runProperties.Append(new Italic());
        }

        if (properties.Underline)
        {
            runProperties.Append(new Underline { Val = UnderlineValues.Single });
        }

        if (properties.Strikethrough)
        {
            runProperties.Append(new Strike());
        }

        var color = NormalizeHex(properties.ColorHex);

        if (color is not null)
        {
            runProperties.Append(new Color { Val = color });
        }

        var highlight = NormalizeHex(properties.HighlightHex);

        if (highlight is not null)
        {
            runProperties.Append(new Shading { Fill = highlight });
        }

        return runProperties;
    }

    private static double? ParseHalfPoints(string? value) =>
        double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;

    private static string? NormalizeHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().TrimStart('#').ToUpperInvariant();

        return trimmed.Length == 6
            && trimmed.All(character => Uri.IsHexDigit(character))
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
    private static ParagraphBlock ToParagraphBlock(
        WpParagraph paragraph,
        Action<string, string> report,
        Numbering? numbering,
        Dictionary<long, Guid> listIds)
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
            var properties = ToRunProperties(run.GetFirstChild<WpRunProperties>());
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
                                "Column breaks are not shown in this preview.");
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
                    default:
                        report(
                            DocxDiagnosticCodes.UnsupportedRunContent,
                            "Some run content is not shown in this preview.");
                        break;
                }
            }

            if (text.Length > 0)
            {
                runs.Add(new CoreRun(Guid.NewGuid(), text.ToString(), properties));
            }
        }

        return new ParagraphBlock(
            Guid.NewGuid(),
            runs.ToImmutable(),
            ToParagraphProperties(
                paragraph.ParagraphProperties,
                numbering,
                listIds));
    }

    private static CoreParagraphProperties? ToParagraphProperties(
        WpParagraphProperties? properties,
        Numbering? numbering,
        Dictionary<long, Guid> listIds)
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
            ?? (ParseTwips(indentation?.Hanging?.Value) is { } hanging
                ? -hanging
                : 0);

        if (alignment == ParagraphAlignment.Left
            && spacing is null
            && indentation is null
            && listKind == ListKind.None
            && properties.ParagraphStyleId?.Val is null)
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
        };
    }

    private static ListKind ResolveListKind(
        Numbering? numbering,
        long? numId,
        int level)
    {
        if (numId is null || numbering is null)
        {
            return ListKind.None;
        }

        var instance = numbering.Elements<NumberingInstance>()
            .FirstOrDefault(candidate =>
                candidate.NumberID?.Value == numId.Value);
        var abstractId = instance?.AbstractNumId?.Val?.Value;
        var abstractNumbering = abstractId is null
            ? null
            : numbering.Elements<AbstractNum>()
                .FirstOrDefault(candidate =>
                    candidate.AbstractNumberId?.Value == abstractId.Value);
        var numberingLevel = abstractNumbering?
            .Elements<Level>()
            .FirstOrDefault(candidate => candidate.LevelIndex?.Value == level);
        var format = numberingLevel?.NumberingFormat?.Val?.Value;

        return format == NumberFormatValues.Bullet
            ? ListKind.Bulleted
            : format is null
                ? ListKind.None
                : ListKind.Numbered;
    }

    private static double? ParseTwips(string? value) =>
        double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed / 20.0
            : null;

    private static double ParseLineSpacing(SpacingBetweenLines? spacing)
    {
        if (!double.TryParse(
                spacing?.Line?.Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var line)
            || line <= 0)
        {
            return 1.15;
        }

        return Math.Clamp(line / 240.0, 0.5, 3.0);
    }
}
