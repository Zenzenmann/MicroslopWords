using System.Collections.Immutable;
using System.Text;
using MicroslopWords.Core.Documents;

namespace MicroslopWords.Layout;

public sealed class LayoutEngine
{
    private readonly double _averageCharacterWidthFactor;

    public LayoutEngine(double averageCharacterWidthFactor = 0.5)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(averageCharacterWidthFactor);

        _averageCharacterWidthFactor = averageCharacterWidthFactor;
    }

    public DocumentLayout Layout(
        Document document,
        PageSettings? pageSettings = null,
        LayoutConstraints? constraints = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var page = pageSettings ?? PageSettings.Letter;
        var contentWidth = constraints?.MaxWidthPoints ?? page.ContentWidthPoints;
        var contentHeight = constraints?.MaxHeightPoints ?? page.ContentHeightPoints;
        var pageBottom = page.MarginTopPoints + contentHeight;

        var pages = ImmutableArray.CreateBuilder<PageLayout>();
        var paragraphs = ImmutableArray.CreateBuilder<ParagraphLayout>();
        var pageNumber = 1;
        var cursorY = page.MarginTopPoints;

        void FlushPage()
        {
            if (paragraphs.Count == 0)
            {
                return;
            }

            pages.Add(new PageLayout(
                pageNumber,
                new LayoutRect(0, 0, page.WidthPoints, page.HeightPoints),
                paragraphs.ToImmutable()));

            paragraphs.Clear();
            pageNumber++;
            cursorY = page.MarginTopPoints;
        }

        foreach (var section in document.Sections)
        {
            foreach (var paragraph in section.Blocks.OfType<ParagraphBlock>())
            {
                var fontSize = paragraph.Runs.Length == 0
                    ? RunProperties.Default.FontSizePoints
                    : paragraph.Runs.Max(run => run.EffectiveProperties.FontSizePoints);
                var lineHeight = fontSize * paragraph.EffectiveProperties.LineSpacing;
                var lines = WrapText(paragraph.PlainText, contentWidth, fontSize);
                var spaceBefore = paragraph.EffectiveProperties.SpaceBeforePoints;
                cursorY += spaceBefore;

                var paragraphLines = ImmutableArray.CreateBuilder<LineLayout>();
                var maxLineWidth = 0d;

                void FinishParagraphSegment()
                {
                    if (paragraphLines.Count == 0)
                    {
                        return;
                    }

                    var paragraphTop = paragraphLines[0].Bounds.Y;
                    var paragraphHeight = Math.Max(lineHeight, paragraphLines.Count * lineHeight);

                    paragraphs.Add(new ParagraphLayout(
                        paragraph.Id,
                        new LayoutRect(page.MarginLeftPoints, paragraphTop, maxLineWidth, paragraphHeight),
                        paragraphLines.ToImmutable()));

                    paragraphLines.Clear();
                    maxLineWidth = 0;
                }

                foreach (var lineText in lines)
                {
                    if (cursorY + lineHeight > pageBottom && (paragraphs.Count > 0 || paragraphLines.Count > 0))
                    {
                        FinishParagraphSegment();
                        FlushPage();
                    }

                    var lineWidth = lineText.Length * fontSize * _averageCharacterWidthFactor;
                    maxLineWidth = Math.Max(maxLineWidth, lineWidth);
                    paragraphLines.Add(new LineLayout(
                        lineText,
                        new LayoutRect(page.MarginLeftPoints, cursorY, lineWidth, lineHeight),
                        fontSize));
                    cursorY += lineHeight;
                }

                FinishParagraphSegment();
                cursorY += paragraph.EffectiveProperties.SpaceAfterPoints;
            }
        }

        FlushPage();

        if (pages.Count == 0)
        {
            pages.Add(new PageLayout(
                1,
                new LayoutRect(0, 0, page.WidthPoints, page.HeightPoints),
                ImmutableArray<ParagraphLayout>.Empty));
        }

        return new DocumentLayout(pages.ToImmutable(), page.WidthPoints, page.HeightPoints);
    }

    private ImmutableArray<string> WrapText(string text, double maxWidth, double fontSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return ImmutableArray.Create(string.Empty);
        }

        var maxCharacters = Math.Max(
            1,
            (int)Math.Floor(maxWidth / (fontSize * _averageCharacterWidthFactor)));

        var words = text.Split(' ', StringSplitOptions.None);
        var lines = ImmutableArray.CreateBuilder<string>();
        var currentLine = new StringBuilder();

        foreach (var word in words)
        {
            var candidateLength = currentLine.Length == 0
                ? word.Length
                : currentLine.Length + 1 + word.Length;

            if (candidateLength > maxCharacters && currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString());
                currentLine.Clear();
                currentLine.Append(word);
            }
            else
            {
                if (currentLine.Length > 0)
                {
                    currentLine.Append(' ');
                }

                currentLine.Append(word);
            }
        }

        lines.Add(currentLine.ToString());
        return lines.ToImmutable();
    }
}
