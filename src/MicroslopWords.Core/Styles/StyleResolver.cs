using MicroslopWords.Core.Documents;

namespace MicroslopWords.Core.Styles;

public static class StyleResolver
{
    public static ParagraphProperties ResolveParagraph(
        ParagraphBlock paragraph,
        StyleSheet styleSheet)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        ArgumentNullException.ThrowIfNull(styleSheet);

        var direct = paragraph.Properties;

        if (direct is null)
        {
            return ParagraphProperties.Default;
        }

        if (string.IsNullOrWhiteSpace(direct.StyleId))
        {
            return direct;
        }

        var style = styleSheet.FindParagraphStyle(direct.StyleId)
            ?? styleSheet.FindParagraphStyle(StyleSheet.NormalStyleId);
        var basis = style?.Properties ?? ParagraphProperties.Default;

        return direct.Overrides == ParagraphPropertyOverrides.None
            ? ApplyNonDefaultParagraphOverrides(basis, direct)
            : ApplyFlaggedParagraphOverrides(basis, direct);
    }

    public static RunProperties ResolveRun(
        Run run,
        ParagraphBlock paragraph,
        StyleSheet styleSheet)
    {
        ArgumentNullException.ThrowIfNull(run);

        return ResolveRunProperties(run.Properties, paragraph, styleSheet);
    }

    public static RunProperties ResolveRunProperties(
        RunProperties? direct,
        ParagraphBlock paragraph,
        StyleSheet styleSheet)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        ArgumentNullException.ThrowIfNull(styleSheet);

        var paragraphStyle = styleSheet.FindParagraphStyle(paragraph.EffectiveProperties.StyleId);
        var paragraphDefaults = paragraphStyle?.CharacterProperties ?? RunProperties.Default;

        if (direct is null)
        {
            return paragraphDefaults;
        }

        var characterStyle = styleSheet.FindCharacterStyle(direct.StyleId)
            ?? styleSheet.FindCharacterStyle(StyleSheet.DefaultCharacterStyleId);
        var basis = characterStyle is null
            ? paragraphDefaults
            : ApplyNonDefaultRunOverrides(paragraphDefaults, characterStyle.Properties);

        if (string.IsNullOrWhiteSpace(direct.StyleId))
        {
            return paragraphStyle is null && characterStyle is null
                ? direct
                : ApplyNonDefaultRunOverrides(basis, direct);
        }

        return direct.Overrides == RunPropertyOverrides.None
            ? ApplyNonDefaultRunOverrides(basis, direct)
            : ApplyFlaggedRunOverrides(basis, direct);
    }

    public static ParagraphPropertyOverrides Diff(
        ParagraphProperties before,
        ParagraphProperties after)
    {
        var overrides = ParagraphPropertyOverrides.None;

        if (before.Alignment != after.Alignment)
        {
            overrides |= ParagraphPropertyOverrides.Alignment;
        }

        if (before.SpaceBeforePoints != after.SpaceBeforePoints)
        {
            overrides |= ParagraphPropertyOverrides.SpaceBefore;
        }

        if (before.SpaceAfterPoints != after.SpaceAfterPoints)
        {
            overrides |= ParagraphPropertyOverrides.SpaceAfter;
        }

        if (before.LineSpacing != after.LineSpacing)
        {
            overrides |= ParagraphPropertyOverrides.LineSpacing;
        }

        if (before.LeftIndentPoints != after.LeftIndentPoints)
        {
            overrides |= ParagraphPropertyOverrides.LeftIndent;
        }

        if (before.RightIndentPoints != after.RightIndentPoints)
        {
            overrides |= ParagraphPropertyOverrides.RightIndent;
        }

        if (before.FirstLineIndentPoints != after.FirstLineIndentPoints)
        {
            overrides |= ParagraphPropertyOverrides.FirstLineIndent;
        }

        if (before.ListKind != after.ListKind
            || before.ListLevel != after.ListLevel
            || before.ListId != after.ListId)
        {
            overrides |= ParagraphPropertyOverrides.List;
        }

        return overrides;
    }

    public static RunPropertyOverrides Diff(RunProperties before, RunProperties after)
    {
        var overrides = RunPropertyOverrides.None;

        if (!string.Equals(before.FontFamily, after.FontFamily, StringComparison.Ordinal))
        {
            overrides |= RunPropertyOverrides.FontFamily;
        }

        if (before.FontSizePoints != after.FontSizePoints)
        {
            overrides |= RunPropertyOverrides.FontSize;
        }

        if (before.Bold != after.Bold)
        {
            overrides |= RunPropertyOverrides.Bold;
        }

        if (before.Italic != after.Italic)
        {
            overrides |= RunPropertyOverrides.Italic;
        }

        if (before.Underline != after.Underline)
        {
            overrides |= RunPropertyOverrides.Underline;
        }

        if (before.Strikethrough != after.Strikethrough)
        {
            overrides |= RunPropertyOverrides.Strikethrough;
        }

        if (!string.Equals(before.ColorHex, after.ColorHex, StringComparison.Ordinal))
        {
            overrides |= RunPropertyOverrides.Color;
        }

        if (!string.Equals(before.HighlightHex, after.HighlightHex, StringComparison.Ordinal))
        {
            overrides |= RunPropertyOverrides.Highlight;
        }

        return overrides;
    }

    public static ParagraphProperties MergeOverrides(
        ParagraphProperties direct,
        ParagraphProperties before,
        ParagraphProperties after)
    {
        var changed = Diff(before, after);
        var result = direct with { Overrides = direct.Overrides | changed };

        if (changed.HasFlag(ParagraphPropertyOverrides.Alignment))
        {
            result = result with { Alignment = after.Alignment };
        }

        if (changed.HasFlag(ParagraphPropertyOverrides.SpaceBefore))
        {
            result = result with { SpaceBeforePoints = after.SpaceBeforePoints };
        }

        if (changed.HasFlag(ParagraphPropertyOverrides.SpaceAfter))
        {
            result = result with { SpaceAfterPoints = after.SpaceAfterPoints };
        }

        if (changed.HasFlag(ParagraphPropertyOverrides.LineSpacing))
        {
            result = result with { LineSpacing = after.LineSpacing };
        }

        if (changed.HasFlag(ParagraphPropertyOverrides.LeftIndent))
        {
            result = result with { LeftIndentPoints = after.LeftIndentPoints };
        }

        if (changed.HasFlag(ParagraphPropertyOverrides.RightIndent))
        {
            result = result with { RightIndentPoints = after.RightIndentPoints };
        }

        if (changed.HasFlag(ParagraphPropertyOverrides.FirstLineIndent))
        {
            result = result with { FirstLineIndentPoints = after.FirstLineIndentPoints };
        }

        if (changed.HasFlag(ParagraphPropertyOverrides.List))
        {
            result = result with
            {
                ListKind = after.ListKind,
                ListLevel = after.ListLevel,
                ListId = after.ListId,
            };
        }

        return result;
    }

    public static RunProperties MergeOverrides(
        RunProperties direct,
        RunProperties before,
        RunProperties after)
    {
        var changed = Diff(before, after);
        var result = direct with { Overrides = direct.Overrides | changed };

        if (changed.HasFlag(RunPropertyOverrides.FontFamily))
        {
            result = result with { FontFamily = after.FontFamily };
        }

        if (changed.HasFlag(RunPropertyOverrides.FontSize))
        {
            result = result with { FontSizePoints = after.FontSizePoints };
        }

        if (changed.HasFlag(RunPropertyOverrides.Bold))
        {
            result = result with { Bold = after.Bold };
        }

        if (changed.HasFlag(RunPropertyOverrides.Italic))
        {
            result = result with { Italic = after.Italic };
        }

        if (changed.HasFlag(RunPropertyOverrides.Underline))
        {
            result = result with { Underline = after.Underline };
        }

        if (changed.HasFlag(RunPropertyOverrides.Strikethrough))
        {
            result = result with { Strikethrough = after.Strikethrough };
        }

        if (changed.HasFlag(RunPropertyOverrides.Color))
        {
            result = result with { ColorHex = after.ColorHex };
        }

        if (changed.HasFlag(RunPropertyOverrides.Highlight))
        {
            result = result with { HighlightHex = after.HighlightHex };
        }

        return result;
    }

    private static ParagraphProperties ApplyNonDefaultParagraphOverrides(
        ParagraphProperties basis,
        ParagraphProperties direct)
    {
        var result = basis;

        if (direct.Alignment != ParagraphProperties.Default.Alignment)
        {
            result = result with { Alignment = direct.Alignment };
        }

        if (direct.SpaceBeforePoints != ParagraphProperties.Default.SpaceBeforePoints)
        {
            result = result with { SpaceBeforePoints = direct.SpaceBeforePoints };
        }

        if (direct.SpaceAfterPoints != ParagraphProperties.Default.SpaceAfterPoints)
        {
            result = result with { SpaceAfterPoints = direct.SpaceAfterPoints };
        }

        if (direct.LineSpacing != ParagraphProperties.Default.LineSpacing)
        {
            result = result with { LineSpacing = direct.LineSpacing };
        }

        if (direct.LeftIndentPoints != ParagraphProperties.Default.LeftIndentPoints)
        {
            result = result with { LeftIndentPoints = direct.LeftIndentPoints };
        }

        if (direct.RightIndentPoints != ParagraphProperties.Default.RightIndentPoints)
        {
            result = result with { RightIndentPoints = direct.RightIndentPoints };
        }

        if (direct.FirstLineIndentPoints != ParagraphProperties.Default.FirstLineIndentPoints)
        {
            result = result with { FirstLineIndentPoints = direct.FirstLineIndentPoints };
        }

        if (direct.ListKind != ParagraphProperties.Default.ListKind
            || direct.ListLevel != ParagraphProperties.Default.ListLevel
            || direct.ListId != ParagraphProperties.Default.ListId)
        {
            result = result with
            {
                ListKind = direct.ListKind,
                ListLevel = direct.ListLevel,
                ListId = direct.ListId,
            };
        }

        return result with { StyleId = direct.StyleId };
    }

    private static ParagraphProperties ApplyFlaggedParagraphOverrides(
        ParagraphProperties basis,
        ParagraphProperties direct)
    {
        var result = basis;

        if (direct.Overrides.HasFlag(ParagraphPropertyOverrides.Alignment))
        {
            result = result with { Alignment = direct.Alignment };
        }

        if (direct.Overrides.HasFlag(ParagraphPropertyOverrides.SpaceBefore))
        {
            result = result with { SpaceBeforePoints = direct.SpaceBeforePoints };
        }

        if (direct.Overrides.HasFlag(ParagraphPropertyOverrides.SpaceAfter))
        {
            result = result with { SpaceAfterPoints = direct.SpaceAfterPoints };
        }

        if (direct.Overrides.HasFlag(ParagraphPropertyOverrides.LineSpacing))
        {
            result = result with { LineSpacing = direct.LineSpacing };
        }

        if (direct.Overrides.HasFlag(ParagraphPropertyOverrides.LeftIndent))
        {
            result = result with { LeftIndentPoints = direct.LeftIndentPoints };
        }

        if (direct.Overrides.HasFlag(ParagraphPropertyOverrides.RightIndent))
        {
            result = result with { RightIndentPoints = direct.RightIndentPoints };
        }

        if (direct.Overrides.HasFlag(ParagraphPropertyOverrides.FirstLineIndent))
        {
            result = result with { FirstLineIndentPoints = direct.FirstLineIndentPoints };
        }

        if (direct.Overrides.HasFlag(ParagraphPropertyOverrides.List))
        {
            result = result with
            {
                ListKind = direct.ListKind,
                ListLevel = direct.ListLevel,
                ListId = direct.ListId,
            };
        }

        return result with { StyleId = direct.StyleId };
    }

    private static RunProperties ApplyNonDefaultRunOverrides(
        RunProperties basis,
        RunProperties direct)
    {
        var result = basis;

        if (!string.Equals(direct.FontFamily, RunProperties.Default.FontFamily, StringComparison.Ordinal))
        {
            result = result with { FontFamily = direct.FontFamily };
        }

        if (direct.FontSizePoints != RunProperties.Default.FontSizePoints)
        {
            result = result with { FontSizePoints = direct.FontSizePoints };
        }

        if (direct.Bold != RunProperties.Default.Bold)
        {
            result = result with { Bold = direct.Bold };
        }

        if (direct.Italic != RunProperties.Default.Italic)
        {
            result = result with { Italic = direct.Italic };
        }

        if (direct.Underline != RunProperties.Default.Underline)
        {
            result = result with { Underline = direct.Underline };
        }

        if (direct.Strikethrough != RunProperties.Default.Strikethrough)
        {
            result = result with { Strikethrough = direct.Strikethrough };
        }

        if (!string.Equals(direct.ColorHex, RunProperties.Default.ColorHex, StringComparison.Ordinal))
        {
            result = result with { ColorHex = direct.ColorHex };
        }

        if (direct.HighlightHex is not null)
        {
            result = result with { HighlightHex = direct.HighlightHex };
        }

        return result with { StyleId = direct.StyleId };
    }

    private static RunProperties ApplyFlaggedRunOverrides(
        RunProperties basis,
        RunProperties direct)
    {
        var result = basis;

        if (direct.Overrides.HasFlag(RunPropertyOverrides.FontFamily))
        {
            result = result with { FontFamily = direct.FontFamily };
        }

        if (direct.Overrides.HasFlag(RunPropertyOverrides.FontSize))
        {
            result = result with { FontSizePoints = direct.FontSizePoints };
        }

        if (direct.Overrides.HasFlag(RunPropertyOverrides.Bold))
        {
            result = result with { Bold = direct.Bold };
        }

        if (direct.Overrides.HasFlag(RunPropertyOverrides.Italic))
        {
            result = result with { Italic = direct.Italic };
        }

        if (direct.Overrides.HasFlag(RunPropertyOverrides.Underline))
        {
            result = result with { Underline = direct.Underline };
        }

        if (direct.Overrides.HasFlag(RunPropertyOverrides.Strikethrough))
        {
            result = result with { Strikethrough = direct.Strikethrough };
        }

        if (direct.Overrides.HasFlag(RunPropertyOverrides.Color))
        {
            result = result with { ColorHex = direct.ColorHex };
        }

        if (direct.Overrides.HasFlag(RunPropertyOverrides.Highlight))
        {
            result = result with { HighlightHex = direct.HighlightHex };
        }

        return result with { StyleId = direct.StyleId };
    }
}
