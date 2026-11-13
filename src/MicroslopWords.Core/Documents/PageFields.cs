using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace MicroslopWords.Core.Documents;

public enum HeaderFooterKind
{
    Header,
    Footer,
}

public enum HeaderFooterVariant
{
    Default,
    FirstPage,
}

public enum PageFieldKind
{
    PageNumber,
    TotalPages,
}

public enum PageNumberFormat
{
    DecimalNumber,
    UpperRoman,
    LowerRoman,
    UpperAlphabetic,
    LowerAlphabetic,
}

public readonly record struct HeaderFooterZone(
    int SectionIndex,
    HeaderFooterKind Kind,
    HeaderFooterVariant Variant);

public sealed record HeaderFooter(ImmutableArray<Block> Blocks)
{
    public static HeaderFooter Empty { get; } = new([]);

    public static HeaderFooter CreateEmpty() =>
        new(ImmutableArray.Create<Block>(ParagraphBlock.Create()));

    public bool IsEmpty => Blocks.Length == 0;

    public bool HasVisibleContent =>
        Blocks.Any(block => block switch
        {
            ParagraphBlock paragraph => paragraph.Runs.Any(run =>
                run.Text.Length > 0
                || run.EffectiveImage is not null
                || run.PageField is not null),
            TableBlock => true,
            _ => false,
        });

    public string PlainText =>
        string.Join(
            '\n',
            Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.PlainText));
}

public static class PageNumberFormatter
{
    public static string Format(int number, PageNumberFormat format)
    {
        var value = Math.Max(1, number);

        return format switch
        {
            PageNumberFormat.UpperRoman => ToRoman(value),
            PageNumberFormat.LowerRoman => ToRoman(value).ToLowerInvariant(),
            PageNumberFormat.UpperAlphabetic => ToAlphabetic(value).ToUpperInvariant(),
            PageNumberFormat.LowerAlphabetic => ToAlphabetic(value),
            _ => value.ToString(CultureInfo.InvariantCulture),
        };
    }

    private static string ToRoman(int value)
    {
        ReadOnlySpan<(int Value, string Symbol)> symbols =
        [
            (1000, "M"),
            (900, "CM"),
            (500, "D"),
            (400, "CD"),
            (100, "C"),
            (90, "XC"),
            (50, "L"),
            (40, "XL"),
            (10, "X"),
            (9, "IX"),
            (5, "V"),
            (4, "IV"),
            (1, "I"),
        ];

        var builder = new StringBuilder();
        var remaining = value;

        foreach (var (amount, symbol) in symbols)
        {
            while (remaining >= amount)
            {
                builder.Append(symbol);
                remaining -= amount;
            }
        }

        return builder.ToString();
    }

    private static string ToAlphabetic(int value)
    {
        var builder = new StringBuilder();
        var remaining = value;

        while (remaining > 0)
        {
            remaining--;
            builder.Insert(0, (char)('a' + (remaining % 26)));
            remaining /= 26;
        }

        return builder.ToString();
    }
}
