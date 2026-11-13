using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using MicroslopWords.Core.Styles;
using Shouldly;

namespace MicroslopWords.Core.Tests;

public sealed class StyleCommandTests
{
    [Fact]
    public void StyleSheet_Default_contains_required_builtins()
    {
        var styles = StyleSheet.Default;

        styles.FindParagraphStyle("Normal").ShouldNotBeNull();
        styles.FindParagraphStyle("Title").ShouldNotBeNull();
        styles.FindParagraphStyle("Heading1").ShouldNotBeNull();
        styles.FindParagraphStyle("Heading2").ShouldNotBeNull();
        styles.FindParagraphStyle("Heading3").ShouldNotBeNull();
        styles.FindCharacterStyle(StyleSheet.DefaultCharacterStyleId).ShouldNotBeNull();
    }

    [Fact]
    public void ApplyParagraphStyleCommand_sets_style_and_undo_restores()
    {
        var (document, paragraph, _) = CreateDocument();
        var selection = SelectionFor(paragraph, 0, paragraph.PlainText.Length);
        var command = new ApplyParagraphStyleCommand(selection, "Heading1");

        var after = command.Apply(document);

        after.FindParagraph(paragraph.Id)!.Properties!.StyleId.ShouldBe("Heading1");
        StyleResolver.ResolveParagraph(
            after.FindParagraph(paragraph.Id)!,
            after.EffectiveStyles).SpaceBeforePoints.ShouldBe(18);
        command.Revert(after).ShouldBeSameAs(document);
    }

    [Fact]
    public void ApplyCharacterStyleCommand_sets_run_style_and_undo_restores()
    {
        var (document, paragraph, run) = CreateDocument();
        var selection = SelectionFor(paragraph, 0, run.Text.Length);
        var command = new ApplyCharacterStyleCommand(selection, "Emphasis");

        var after = command.Apply(document);

        var updatedRun = after.FindParagraph(paragraph.Id)!.Runs.Single();
        updatedRun.Properties!.StyleId.ShouldBe("Emphasis");
        StyleResolver.ResolveRun(
            updatedRun,
            after.FindParagraph(paragraph.Id)!,
            after.EffectiveStyles).Italic.ShouldBeTrue();
        command.Revert(after).ShouldBeSameAs(document);
    }

    [Fact]
    public void CreateParagraphStyleCommand_rejects_duplicate_name_case_insensitively()
    {
        var (document, paragraph, _) = CreateDocument();
        var selection = SelectionFor(paragraph, 0, 0);

        Should.Throw<InvalidOperationException>(() =>
            new CreateParagraphStyleCommand(selection, "heading 1").Apply(document));
    }

    [Fact]
    public void CreateParagraphStyleCommand_adds_style_and_applies_it()
    {
        var (document, paragraph, _) = CreateDocument();
        var selection = SelectionFor(paragraph, 0, paragraph.PlainText.Length);
        var command = new CreateParagraphStyleCommand(selection, "Project heading");

        var after = command.Apply(document);

        after.EffectiveStyles.FindParagraphStyle(command.StyleId).ShouldNotBeNull();
        after.FindParagraph(paragraph.Id)!.Properties!.StyleId.ShouldBe(command.StyleId);
    }

    [Fact]
    public void EditParagraphStyleCommand_updates_styled_users_and_keeps_direct_overrides()
    {
        var (document, paragraph, run) = CreateDocument();
        var selection = SelectionFor(paragraph, 0, paragraph.PlainText.Length);
        var styled = new ApplyParagraphStyleCommand(selection, "Heading1").Apply(document);
        var styledParagraph = styled.FindParagraph(paragraph.Id)!;
        var boldRun = styledParagraph.Runs.Single() with
        {
            Properties = new RunProperties
            {
                Bold = true,
                Overrides = RunPropertyOverrides.Bold,
            },
        };
        styled = styled.ReplaceBlock(styledParagraph with { Runs = [boldRun] });
        var edited = new EditParagraphStyleCommand(
            selection,
            "Heading1",
            ParagraphProperties.Default with { SpaceBeforePoints = 99 },
            RunProperties.Default with { FontSizePoints = 22 }).Apply(styled);

        var resultParagraph = edited.FindParagraph(paragraph.Id)!;
        var resolvedParagraph = StyleResolver.ResolveParagraph(resultParagraph, edited.EffectiveStyles);
        var resolvedRun = StyleResolver.ResolveRun(
            resultParagraph.Runs.Single(),
            resultParagraph,
            edited.EffectiveStyles);

        resolvedParagraph.SpaceBeforePoints.ShouldBe(99);
        resolvedRun.FontSizePoints.ShouldBe(22);
        resolvedRun.Bold.ShouldBeTrue();
    }

    [Fact]
    public void RenameStyleCommand_updates_name_without_changing_references()
    {
        var (document, paragraph, _) = CreateDocument();
        var selection = SelectionFor(paragraph, 0, paragraph.PlainText.Length);
        var styled = new ApplyParagraphStyleCommand(selection, "Heading1").Apply(document);
        var command = new RenameStyleCommand(selection, "Heading1", "Section title");

        var renamed = command.Apply(styled);

        renamed.EffectiveStyles.FindParagraphStyle("Heading1")!.Name.ShouldBe("Section title");
        renamed.FindParagraph(paragraph.Id)!.Properties!.StyleId.ShouldBe("Heading1");
    }

    [Fact]
    public void DeleteStyleCommand_reassigns_paragraphs_to_normal_and_removes_style()
    {
        var (document, paragraph, _) = CreateDocument();
        var selection = SelectionFor(paragraph, 0, paragraph.PlainText.Length);
        var create = new CreateParagraphStyleCommand(selection, "Temporary");
        var created = create.Apply(document);
        var command = new DeleteStyleCommand(selection, create.StyleId);

        var deleted = command.Apply(created);

        deleted.EffectiveStyles.FindParagraphStyle(create.StyleId).ShouldBeNull();
        deleted.FindParagraph(paragraph.Id)!.Properties!.StyleId.ShouldBe(
            StyleSheet.NormalStyleId);
    }

    [Fact]
    public void DeleteStyleCommand_reassigns_character_style_to_default()
    {
        var (document, paragraph, run) = CreateDocument();
        var selection = SelectionFor(paragraph, 0, run.Text.Length);
        var styled = new ApplyCharacterStyleCommand(selection, "Emphasis").Apply(document);
        var command = new DeleteStyleCommand(selection, "Emphasis");

        var deleted = command.Apply(styled);

        deleted.EffectiveStyles.FindCharacterStyle("Emphasis").ShouldBeNull();
        deleted.FindParagraph(paragraph.Id)!.Runs.Single().Properties!.StyleId.ShouldBe(
            StyleSheet.DefaultCharacterStyleId);
    }

    [Fact]
    public void DeleteStyleCommand_rejects_normal_and_default_character_styles()
    {
        var (document, paragraph, _) = CreateDocument();
        var selection = SelectionFor(paragraph, 0, 0);

        Should.Throw<InvalidOperationException>(() =>
            new DeleteStyleCommand(selection, StyleSheet.NormalStyleId).Apply(document));
        Should.Throw<InvalidOperationException>(() =>
            new DeleteStyleCommand(selection, StyleSheet.DefaultCharacterStyleId).Apply(document));
    }

    [Fact]
    public void ApplyParagraphStyleCommand_preserves_list_membership()
    {
        var (document, paragraph, _) = CreateDocument();
        var listId = Guid.NewGuid();
        document = document.ReplaceBlock(paragraph with
        {
            Properties = new ParagraphProperties
            {
                ListKind = ListKind.Bulleted,
                ListLevel = 2,
                ListId = listId,
            },
        });
        var selection = SelectionFor(paragraph, 0, paragraph.PlainText.Length);

        var styled = new ApplyParagraphStyleCommand(selection, "Heading1").Apply(document);
        var resolved = StyleResolver.ResolveParagraph(
            styled.FindParagraph(paragraph.Id)!,
            styled.EffectiveStyles);

        resolved.StyleId.ShouldBe("Heading1");
        resolved.ListKind.ShouldBe(ListKind.Bulleted);
        resolved.ListLevel.ShouldBe(2);
        resolved.ListId.ShouldBe(listId);
    }

    [Fact]
    public void ClearParagraphFormattingCommand_keeps_style_and_list()
    {
        var (document, paragraph, _) = CreateDocument();
        var selection = SelectionFor(paragraph, 0, paragraph.PlainText.Length);
        var styled = new ApplyParagraphStyleCommand(selection, "Heading1").Apply(document);
        var formatted = new ApplyParagraphPropertiesCommand(
            selection,
            properties => properties with { SpaceBeforePoints = 99 }).Apply(styled);

        var cleared = new ClearParagraphFormattingCommand(selection).Apply(formatted);
        var resolved = StyleResolver.ResolveParagraph(
            cleared.FindParagraph(paragraph.Id)!,
            cleared.EffectiveStyles);

        resolved.StyleId.ShouldBe("Heading1");
        resolved.SpaceBeforePoints.ShouldBe(18);
    }

    [Fact]
    public void ClearCharacterFormattingCommand_keeps_character_style()
    {
        var (document, paragraph, run) = CreateDocument();
        var selection = SelectionFor(paragraph, 0, run.Text.Length);
        var styled = new ApplyCharacterStyleCommand(selection, "Emphasis").Apply(document);
        var formatted = new FormatSelectionCommand(
            selection,
            properties => properties with { Bold = true }).Apply(styled);

        var cleared = new ClearCharacterFormattingCommand(selection).Apply(formatted);
        var updatedRun = cleared.FindParagraph(paragraph.Id)!.Runs.Single();
        var resolved = StyleResolver.ResolveRun(
            updatedRun,
            cleared.FindParagraph(paragraph.Id)!,
            cleared.EffectiveStyles);

        updatedRun.Properties!.StyleId.ShouldBe("Emphasis");
        resolved.Italic.ShouldBeTrue();
        resolved.Bold.ShouldBeFalse();
    }

    [Fact]
    public void CreateParagraphStyleCommand_captures_current_formatting()
    {
        var (document, paragraph, _) = CreateDocument();
        var selection = SelectionFor(paragraph, 0, paragraph.PlainText.Length);
        var formatted = new ApplyParagraphPropertiesCommand(
            selection,
            properties => properties with
            {
                Alignment = ParagraphAlignment.Right,
                SpaceAfterPoints = 14,
            }).Apply(document);

        var command = new CreateParagraphStyleCommand(selection, "Captured");
        var created = command.Apply(formatted);
        var style = created.EffectiveStyles.FindParagraphStyle(command.StyleId)!;

        style.Properties.Alignment.ShouldBe(ParagraphAlignment.Right);
        style.Properties.SpaceAfterPoints.ShouldBe(14);
    }

    [Fact]
    public void RenameStyleCommand_rejects_an_existing_name()
    {
        var (document, paragraph, _) = CreateDocument();
        var selection = SelectionFor(paragraph, 0, 0);

        Should.Throw<InvalidOperationException>(() =>
            new RenameStyleCommand(selection, "Heading1", "Title").Apply(document));
    }

    [Fact]
    public void StyleResolver_falls_back_to_normal_for_unknown_paragraph_style()
    {
        var (document, paragraph, _) = CreateDocument();
        var styled = paragraph with
        {
            Properties = new ParagraphProperties { StyleId = "MissingStyle" },
        };
        document = document.ReplaceBlock(styled);

        var resolved = StyleResolver.ResolveParagraph(styled, document.EffectiveStyles);

        resolved.StyleId.ShouldBe("MissingStyle");
        resolved.SpaceAfterPoints.ShouldBe(ParagraphProperties.Default.SpaceAfterPoints);
    }

    private static (Document Document, ParagraphBlock Paragraph, Run Run) CreateDocument()
    {
        var run = Run.Create("Hello world");
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(run));
        var section = new Section(Guid.NewGuid(), ImmutableArray.Create<Block>(paragraph));
        var document = new Document(
            Guid.NewGuid(),
            "Styles",
            ImmutableArray.Create(section),
            StyleSheet.Default);

        return (document, paragraph, run);
    }

    private static Selection SelectionFor(
        ParagraphBlock paragraph,
        int start,
        int end) =>
        new(
            new TextPosition(paragraph.Id, paragraph.Runs[0].Id, start),
            new TextPosition(paragraph.Id, paragraph.Runs[0].Id, end));
}
