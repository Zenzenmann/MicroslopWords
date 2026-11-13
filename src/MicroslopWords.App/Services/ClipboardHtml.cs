using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MicroslopWords.Core.Documents;

namespace MicroslopWords.App.Services;

public static partial class ClipboardHtml
{
    public static string ToHtml(DocumentFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);

        var builder = new StringBuilder();

        foreach (var paragraph in fragment.Paragraphs)
        {
            builder.Append("<p>");

            foreach (var run in paragraph.Runs)
            {
                var properties = run.Properties;

                if (properties?.Underline == true)
                {
                    builder.Append("<u>");
                }

                if (properties?.Italic == true)
                {
                    builder.Append("<i>");
                }

                if (properties?.Bold == true)
                {
                    builder.Append("<b>");
                }

                var encoded = WebUtility.HtmlEncode(run.Text)
                    .Replace("\n", "<br>", StringComparison.Ordinal)
                    .Replace(
                        InlineImage.PlaceholderCharacter.ToString(),
                        string.Empty,
                        StringComparison.Ordinal);
                builder.Append(encoded);

                if (properties?.Bold == true)
                {
                    builder.Append("</b>");
                }

                if (properties?.Italic == true)
                {
                    builder.Append("</i>");
                }

                if (properties?.Underline == true)
                {
                    builder.Append("</u>");
                }
            }

            builder.Append("</p>");
        }

        return builder.ToString();
    }

    public static DocumentFragment Parse(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return DocumentFragment.Empty;
        }

        var sanitized = HtmlComment().Replace(html, string.Empty);
        sanitized = DangerousHtmlBlock().Replace(sanitized, string.Empty);

        var paragraphs = new List<ParagraphFragment>();
        var runs = new List<RunFragment>();
        var text = new StringBuilder();
        var bold = 0;
        var italic = 0;
        var underline = 0;
        string? listType = null;
        var listIndex = 0;

        void FlushRun()
        {
            if (text.Length == 0)
            {
                return;
            }

            var properties = bold == 0 && italic == 0 && underline == 0
                ? null
                : new RunProperties
                {
                    Bold = bold > 0,
                    Italic = italic > 0,
                    Underline = underline > 0,
                };

            runs.Add(new RunFragment(text.ToString(), properties));
            text.Clear();
        }

        void FlushParagraph(bool force = false)
        {
            FlushRun();

            if (runs.Count > 0 || force)
            {
                paragraphs.Add(new ParagraphFragment(
                    null,
                    runs.ToImmutableArray()));
            }

            runs.Clear();
        }

        foreach (Match token in HtmlToken().Matches(sanitized))
        {
            if (!token.Value.StartsWith('<'))
            {
                text.Append(WebUtility.HtmlDecode(token.Value));
                continue;
            }

            var match = HtmlTagName().Match(token.Value);

            if (!match.Success)
            {
                continue;
            }

            var closing = match.Groups[1].Value == "/";
            var name = match.Groups[2].Value.ToLowerInvariant();

            switch (name)
            {
                case "b":
                case "strong":
                    FlushRun();
                    bold = closing ? Math.Max(0, bold - 1) : bold + 1;
                    break;
                case "i":
                case "em":
                    FlushRun();
                    italic = closing ? Math.Max(0, italic - 1) : italic + 1;
                    break;
                case "u":
                    FlushRun();
                    underline = closing ? Math.Max(0, underline - 1) : underline + 1;
                    break;
                case "br":
                    text.Append('\n');
                    break;
                case "p":
                case "div":
                case "blockquote":
                case "pre":
                case "tr":
                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    FlushParagraph();
                    break;
                case "ul":
                    FlushParagraph();
                    listType = closing ? null : "ul";
                    listIndex = 0;
                    break;
                case "ol":
                    FlushParagraph();
                    listType = closing ? null : "ol";
                    listIndex = 0;
                    break;
                case "li":
                    FlushParagraph();

                    if (!closing && listType is not null)
                    {
                        text.Append(listType == "ol"
                            ? $"{++listIndex}. "
                            : "\u2022 ");
                    }

                    break;
            }
        }

        FlushParagraph();

        return paragraphs.Count == 0
            ? DocumentFragment.Empty
            : new DocumentFragment(paragraphs.ToImmutableArray());
    }

    [GeneratedRegex(@"(?s)<!--.*?-->", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlComment();

    [GeneratedRegex(
        @"(?is)<(script|style|head|title|noscript|template)\b[^>]*>.*?</\1\s*>",
        RegexOptions.CultureInvariant)]
    private static partial Regex DangerousHtmlBlock();

    [GeneratedRegex(@"(?s)<[^>]*>|[^<]+", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlToken();

    [GeneratedRegex(@"^<\s*(/?)\s*([a-zA-Z0-9]+)", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagName();
}
