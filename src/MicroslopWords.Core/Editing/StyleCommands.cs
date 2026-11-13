using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Styles;

namespace MicroslopWords.Core.Editing;

public sealed class ApplyParagraphStyleCommand(
    Selection selection,
    string styleId) : DocumentEditCommand
{
    public override string Description => "Apply paragraph style";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);
        var sheet = document.EffectiveStyles;

        if (sheet.FindParagraphStyle(styleId) is null)
        {
            throw new InvalidOperationException($"Paragraph style '{styleId}' was not found.");
        }

        var result = document;

        foreach (var paragraph in ParagraphFormatting.GetAffectedParagraphs(document, current))
        {
            var effective = StyleResolver.ResolveParagraph(paragraph, sheet);
            var hasList = effective.ListKind != ListKind.None;

            result = result.ReplaceBlock(paragraph with
            {
                Properties = new ParagraphProperties
                {
                    StyleId = styleId,
                    ListKind = effective.ListKind,
                    ListLevel = effective.ListLevel,
                    ListId = effective.ListId,
                    Overrides = hasList
                        ? ParagraphPropertyOverrides.List
                        : ParagraphPropertyOverrides.None,
                },
            });
        }

        selectionAfter = current;
        return result;
    }
}

public sealed class ApplyCharacterStyleCommand(
    Selection selection,
    string styleId) : DocumentEditCommand
{
    public override string Description => "Apply character style";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);
        var sheet = document.EffectiveStyles;

        if (sheet.FindCharacterStyle(styleId) is null)
        {
            throw new InvalidOperationException($"Character style '{styleId}' was not found.");
        }

        if (current.IsCollapsed)
        {
            selectionAfter = current;
            return document;
        }

        var (result, mapped) = CharacterFormatting.Apply(
            document,
            current,
            _ => new RunProperties { StyleId = styleId });

        selectionAfter = mapped;
        return result;
    }
}

public sealed class CreateParagraphStyleCommand(
    Selection selection,
    string name) : DocumentEditCommand
{
    public string StyleId { get; } = "User" + Guid.NewGuid().ToString("N")[..8];

    public override string Description => "Create paragraph style";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);
        var sheet = document.EffectiveStyles;
        var normalizedName = StyleCommands.NormalizeName(name);
        StyleCommands.EnsureUniqueName(sheet, normalizedName);

        var paragraphs = ParagraphFormatting.GetAffectedParagraphs(document, current);
        var first = paragraphs.FirstOrDefault();
        var paragraphProperties = first is null
            ? ParagraphProperties.Default
            : StyleResolver.ResolveParagraph(first, sheet);
        var characterProperties = first?.Runs.Length > 0
            ? StyleResolver.ResolveRun(first.Runs[0], first, sheet)
            : first is null
                ? RunProperties.Default
                : sheet.FindParagraphStyle(first.EffectiveProperties.StyleId)?.CharacterProperties
                    ?? RunProperties.Default;

        var style = new ParagraphStyle(
            StyleId,
            normalizedName,
            paragraphProperties,
            characterProperties);
        var result = document with
        {
            Styles = sheet with
            {
                ParagraphStyles = sheet.ParagraphStyles.Add(style),
            },
        };

        if (first is not null)
        {
            var applied = new ApplyParagraphStyleCommand(current, StyleId).Apply(result);
            selectionAfter = current;

            return applied;
        }

        selectionAfter = current;
        return result;
    }
}

public sealed class CreateCharacterStyleCommand(
    Selection selection,
    string name,
    RunProperties? properties = null) : DocumentEditCommand
{
    public string StyleId { get; } = "User" + Guid.NewGuid().ToString("N")[..8];

    public override string Description => "Create character style";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);
        var sheet = document.EffectiveStyles;
        var normalizedName = StyleCommands.NormalizeName(name);
        StyleCommands.EnsureUniqueName(sheet, normalizedName);

        var styleProperties = properties ?? StyleCommands.GetRunProperties(document, current);
        var style = new CharacterStyle(StyleId, normalizedName, styleProperties);
        var result = document with
        {
            Styles = sheet with
            {
                CharacterStyles = sheet.CharacterStyles.Add(style),
            },
        };

        if (!current.IsCollapsed)
        {
            result = new ApplyCharacterStyleCommand(current, StyleId).Apply(result);
        }

        selectionAfter = current;
        return result;
    }
}

public sealed class EditParagraphStyleCommand(
    Selection selection,
    string styleId,
    ParagraphProperties properties,
    RunProperties? characterProperties) : DocumentEditCommand
{
    public override string Description => "Edit paragraph style";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);
        var sheet = document.EffectiveStyles;
        var style = sheet.FindParagraphStyle(styleId)
            ?? throw new InvalidOperationException($"Paragraph style '{styleId}' was not found.");
        var updated = style with
        {
            Properties = properties,
            CharacterProperties = characterProperties,
        };

        selectionAfter = current;

        return document with
        {
            Styles = sheet with
            {
                ParagraphStyles = StyleCommands.ReplaceParagraphStyle(sheet.ParagraphStyles, updated),
            },
        };
    }
}

public sealed class EditCharacterStyleCommand(
    Selection selection,
    string styleId,
    RunProperties properties) : DocumentEditCommand
{
    public override string Description => "Edit character style";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);
        var sheet = document.EffectiveStyles;
        var style = sheet.FindCharacterStyle(styleId)
            ?? throw new InvalidOperationException($"Character style '{styleId}' was not found.");
        var updated = style with { Properties = properties };

        selectionAfter = current;

        return document with
        {
            Styles = sheet with
            {
                CharacterStyles = StyleCommands.ReplaceCharacterStyle(sheet.CharacterStyles, updated),
            },
        };
    }
}

public sealed class RenameStyleCommand(
    Selection selection,
    string styleId,
    string name) : DocumentEditCommand
{
    public override string Description => "Rename style";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);
        var sheet = document.EffectiveStyles;
        var normalizedName = StyleCommands.NormalizeName(name);
        StyleCommands.EnsureUniqueName(sheet, normalizedName, styleId);
        var paragraph = sheet.FindParagraphStyle(styleId);
        var character = sheet.FindCharacterStyle(styleId);

        if (paragraph is null && character is null)
        {
            throw new InvalidOperationException($"Style '{styleId}' was not found.");
        }

        var updated = sheet;

        if (paragraph is not null)
        {
            updated = updated with
            {
                ParagraphStyles = StyleCommands.ReplaceParagraphStyle(
                    updated.ParagraphStyles,
                    paragraph with { Name = normalizedName }),
            };
        }

        if (character is not null)
        {
            updated = updated with
            {
                CharacterStyles = StyleCommands.ReplaceCharacterStyle(
                    updated.CharacterStyles,
                    character with { Name = normalizedName }),
            };
        }

        selectionAfter = current;

        return document with { Styles = updated };
    }
}

public sealed class DeleteStyleCommand(
    Selection selection,
    string styleId) : DocumentEditCommand
{
    public override string Description => "Delete style";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        if (styleId is StyleSheet.NormalStyleId or StyleSheet.DefaultCharacterStyleId)
        {
            throw new InvalidOperationException("The default style cannot be deleted.");
        }

        var current = SelectionMap.Normalize(selection, document);
        var sheet = document.EffectiveStyles;
        var paragraphStyle = sheet.FindParagraphStyle(styleId);
        var characterStyle = sheet.FindCharacterStyle(styleId);

        if (paragraphStyle is null && characterStyle is null)
        {
            throw new InvalidOperationException($"Style '{styleId}' was not found.");
        }

        var result = document;

        if (paragraphStyle is not null)
        {
            foreach (var paragraph in ParagraphFormatting.GetAllParagraphs(document))
            {
                if (!string.Equals(
                        paragraph.Properties?.StyleId,
                        styleId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                result = result.ReplaceBlock(paragraph with
                {
                    Properties = paragraph.Properties! with
                    {
                        StyleId = StyleSheet.NormalStyleId,
                    },
                });
            }

            sheet = sheet with
            {
                ParagraphStyles = sheet.ParagraphStyles.Remove(paragraphStyle),
            };
        }

        if (characterStyle is not null)
        {
            foreach (var paragraph in ParagraphFormatting.GetAllParagraphs(result))
            {
                var runs = paragraph.Runs;
                var changed = false;

                for (var index = 0; index < runs.Length; index++)
                {
                    if (!string.Equals(
                            runs[index].Properties?.StyleId,
                            styleId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    runs = runs.SetItem(
                        index,
                        runs[index] with
                        {
                            Properties = runs[index].Properties! with
                            {
                                StyleId = StyleSheet.DefaultCharacterStyleId,
                            },
                        });
                    changed = true;
                }

                if (changed)
                {
                    result = result.ReplaceBlock(paragraph with { Runs = runs });
                }
            }

            sheet = sheet with
            {
                CharacterStyles = sheet.CharacterStyles.Remove(characterStyle),
            };
        }

        selectionAfter = current;

        return result with { Styles = sheet };
    }
}

internal static class StyleCommands
{
    public static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Style name is required.");
        }

        return name.Trim();
    }

    public static void EnsureUniqueName(
        StyleSheet sheet,
        string name,
        string? exceptId = null)
    {
        if (sheet.HasStyleName(name, exceptId))
        {
            throw new InvalidOperationException($"A style named '{name}' already exists.");
        }
    }

    public static RunProperties GetRunProperties(Document document, Selection selection)
    {
        if (selection.IsCollapsed)
        {
            var paragraph = document.FindParagraph(selection.Focus.ParagraphId);
            var run = paragraph?.Runs.FirstOrDefault(candidate =>
                candidate.Id == selection.Focus.RunId);

            return run is null || paragraph is null
                ? RunProperties.Default
                : StyleResolver.ResolveRun(run, paragraph, document.EffectiveStyles);
        }

        var start = selection.GetStart(document);
        var paragraphAtStart = document.FindParagraph(start.ParagraphId);
        var runAtStart = paragraphAtStart?.Runs.FirstOrDefault(candidate =>
            candidate.Id == start.RunId);

        return runAtStart is null || paragraphAtStart is null
            ? RunProperties.Default
            : StyleResolver.ResolveRun(
                runAtStart,
                paragraphAtStart,
                document.EffectiveStyles);
    }

    public static ImmutableArray<ParagraphStyle> ReplaceParagraphStyle(
        ImmutableArray<ParagraphStyle> styles,
        ParagraphStyle replacement) =>
        styles
            .Select(style => style.Id == replacement.Id ? replacement : style)
            .ToImmutableArray();

    public static ImmutableArray<CharacterStyle> ReplaceCharacterStyle(
        ImmutableArray<CharacterStyle> styles,
        CharacterStyle replacement) =>
        styles
            .Select(style => style.Id == replacement.Id ? replacement : style)
            .ToImmutableArray();
}
