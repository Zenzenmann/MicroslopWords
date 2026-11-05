using MicroslopWords.Core.Editing;

namespace MicroslopWords.Layout;

public readonly record struct LineHit(
    PageLayout Page,
    int PageIndex,
    int LineIndex,
    ParagraphLayout Paragraph,
    LineLayout Line,
    int ParagraphOffset);

public static class LayoutHitTesting
{
    public static CaretLayout? FindCaret(
        DocumentLayout layout,
        Guid paragraphId,
        int paragraphOffset,
        TextAffinity affinity = TextAffinity.Downstream)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var candidates = new List<CaretLayout>();

        for (var pageIndex = 0; pageIndex < layout.Pages.Length; pageIndex++)
        {
            var page = layout.Pages[pageIndex];
            var lineIndex = 0;

            foreach (var paragraph in page.Paragraphs)
            {
                if (paragraph.ParagraphId != paragraphId)
                {
                    lineIndex += paragraph.Lines.Length;
                    continue;
                }

                foreach (var line in paragraph.Lines)
                {
                    candidates.Add(new CaretLayout(
                        page,
                        lineIndex,
                        line,
                        BuildCaretRect(line, paragraphOffset)));
                    lineIndex++;
                }
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        if (affinity == TextAffinity.Upstream)
        {
            CaretLayout? chosen = null;

            for (var index = 0; index < candidates.Count; index++)
            {
                var line = candidates[index].Line;

                if (paragraphOffset > line.TextStart
                    || (line.Text.Length == 0
                        && paragraphOffset == line.TextStart
                        && chosen is null)
                    || (line.IsAfterSoftBreak
                        && paragraphOffset == line.TextStart))
                {
                    chosen = candidates[index];
                }
            }

            return chosen ?? candidates[0];
        }
        else
        {
            CaretLayout? previous = null;

            foreach (var candidate in candidates)
            {
                var line = candidate.Line;
                var end = line.TextStart + line.Text.Length;

                if (paragraphOffset < line.TextStart)
                {
                    return previous ?? candidate;
                }

                if (paragraphOffset < end
                    || (line.Text.Length == 0 && paragraphOffset == line.TextStart))
                {
                    return candidate;
                }

                previous = candidate;
            }

            return candidates[^1];
        }
    }

    public static LineHit? HitTestLine(
        DocumentLayout layout,
        int pageNumber,
        double x,
        double y)
    {
        ArgumentNullException.ThrowIfNull(layout);

        for (var pageIndex = 0; pageIndex < layout.Pages.Length; pageIndex++)
        {
            var page = layout.Pages[pageIndex];

            if (page.PageNumber != pageNumber)
            {
                continue;
            }

            LineHit? best = null;
            var bestDistance = double.MaxValue;
            var lineIndex = 0;

            foreach (var paragraph in page.Paragraphs)
            {
                foreach (var line in paragraph.Lines)
                {
                    var distance = VerticalDistance(line.Bounds, y);

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = new LineHit(
                            page,
                            pageIndex,
                            lineIndex,
                            paragraph,
                            line,
                            OffsetAtX(line, x));
                    }

                    lineIndex++;
                }
            }

            return best;
        }

        return null;
    }

    public static LineLayout? GetLine(PageLayout page, int lineIndex)
    {
        ArgumentNullException.ThrowIfNull(page);
        var index = 0;

        foreach (var paragraph in page.Paragraphs)
        {
            foreach (var line in paragraph.Lines)
            {
                if (index == lineIndex)
                {
                    return line;
                }

                index++;
            }
        }

        return null;
    }

    public static double GetCaretX(LineLayout line, int paragraphOffset)
    {
        var relative = Math.Clamp(
            paragraphOffset - line.TextStart,
            0,
            line.Text.Length);
        var x = line.Bounds.X;

        foreach (var cluster in line.Clusters)
        {
            var clusterStart = cluster.TextStart - line.TextStart;

            if (relative <= clusterStart)
            {
                break;
            }

            var clusterEnd = clusterStart + cluster.TextLength;

            if (relative >= clusterEnd)
            {
                x += cluster.Advance;
                continue;
            }

            var fraction = (relative - clusterStart) / (double)cluster.TextLength;
            x += cluster.Advance * fraction;
            break;
        }

        return x;
    }

    private static LayoutRect BuildCaretRect(LineLayout line, int paragraphOffset) =>
        new(GetCaretX(line, paragraphOffset), line.Bounds.Y, 1, line.Bounds.Height);

    private static int OffsetAtX(LineLayout line, double x)
    {
        if (x <= line.Bounds.X)
        {
            return line.TextStart;
        }

        if (x >= line.Bounds.Right)
        {
            return line.TextStart + line.Text.Length;
        }

        var relative = x - line.Bounds.X;
        var accumulated = 0d;

        foreach (var cluster in line.Clusters)
        {
            if (relative < accumulated + (cluster.Advance / 2))
            {
                return cluster.TextStart;
            }

            accumulated += cluster.Advance;

            if (relative < accumulated)
            {
                return cluster.TextStart + cluster.TextLength;
            }
        }

        return line.TextStart + line.Text.Length;
    }

    private static double VerticalDistance(LayoutRect bounds, double y)
    {
        if (y < bounds.Y)
        {
            return bounds.Y - y;
        }

        if (y > bounds.Bottom)
        {
            return y - bounds.Bottom;
        }

        return 0;
    }
}
