using System.Collections.Immutable;
using System.Globalization;
using MicroslopWords.Core.Documents;

namespace MicroslopWords.Core.Editing;

/// <summary>Options that control body-text search.</summary>
public readonly record struct FindOptions(
    bool MatchCase = false,
    bool WholeWords = false);

/// <summary>
/// A body-text match, expressed as an offset into one paragraph's plain text.
/// Paragraph offsets stay valid while the surrounding text is being replaced.
/// </summary>
public sealed record SearchMatch(
    Guid ParagraphId,
    int StartOffset,
    int Length,
    string Text)
{
    public int EndOffset => StartOffset + Length;
}

public static class SearchEngine
{
    public static ImmutableArray<SearchMatch> Find(
        Document document,
        string query,
        FindOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(query);

        if (query.Length == 0)
        {
            return [];
        }

        var comparison = options.MatchCase
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        var matches = ImmutableArray.CreateBuilder<SearchMatch>();

        foreach (var paragraph in Paragraphs(document))
        {
            var text = paragraph.PlainText;
            var searchStart = 0;

            while (searchStart <= text.Length - query.Length)
            {
                var index = text.IndexOf(query, searchStart, comparison);

                if (index < 0)
                {
                    break;
                }

                if ((!options.WholeWords || IsWholeWord(text, index, query.Length))
                    && IsGraphemeBoundary(text, index)
                    && IsGraphemeBoundary(text, index + query.Length))
                {
                    matches.Add(new SearchMatch(
                        paragraph.Id,
                        index,
                        query.Length,
                        text.Substring(index, query.Length)));
                }

                searchStart = index + Math.Max(1, query.Length);
            }
        }

        return matches.ToImmutable();
    }

    public static Selection GetSelection(Document document, SearchMatch match)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(match);

        var paragraph = document.FindParagraph(match.ParagraphId)
            ?? throw new ArgumentException(
                "The match paragraph was not found in the document.",
                nameof(match));

        return GetSelection(paragraph, match);
    }

    public static Selection GetSelection(ParagraphBlock paragraph, SearchMatch match)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        ArgumentNullException.ThrowIfNull(match);

        return new Selection(
            TextNavigation.FromParagraphTextOffset(paragraph, match.StartOffset),
            TextNavigation.FromParagraphTextOffset(paragraph, match.EndOffset));
    }

    internal static IEnumerable<ParagraphBlock> Paragraphs(Document document) =>
        document.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>();

    private static bool IsWholeWord(string text, int index, int length)
    {
        var startsWithWord = IsWordCharacter(text[index]);
        var endsWithWord = IsWordCharacter(text[index + length - 1]);

        return (!startsWithWord || index == 0 || !IsWordCharacter(text[index - 1]))
            && (!endsWithWord
                || index + length == text.Length
                || !IsWordCharacter(text[index + length]));
    }

    private static bool IsWordCharacter(char character)
    {
        if (char.IsLetterOrDigit(character) || character == '_')
        {
            return true;
        }

        return CharUnicodeInfo.GetUnicodeCategory(character)
            is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark;
    }

    private static bool IsGraphemeBoundary(string text, int offset) =>
        offset == 0
        || offset == text.Length
        || Array.BinarySearch(StringInfo.ParseCombiningCharacters(text), offset) >= 0;
}
