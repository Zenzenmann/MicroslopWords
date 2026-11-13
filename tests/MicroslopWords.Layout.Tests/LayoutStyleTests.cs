using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Styles;
using Shouldly;

namespace MicroslopWords.Layout.Tests;

public sealed class LayoutStyleTests
{
    [Fact]
    public void Layout_uses_paragraph_style_character_defaults()
    {
        var document = CreateDocument(
            CreateParagraph("Heading text", "Heading1"),
            StyleSheet.Default);

        var layout = new LayoutEngine().Layout(document);
        var line = layout.Pages[0].Paragraphs.Single().Lines.Single();

        line.FontSizePoints.ShouldBe(18);
        line.Spans.Single().FontSizePoints.ShouldBe(18);
    }

    [Fact]
    public void Layout_uses_character_style_over_paragraph_style()
    {
        var run = Run.Create(
            "Styled",
            new RunProperties { StyleId = "Emphasis" });
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(run),
            new ParagraphProperties { StyleId = "Heading1" });
        var document = CreateDocument(paragraph, StyleSheet.Default);

        var layout = new LayoutEngine().Layout(document);
        var line = layout.Pages[0].Paragraphs.Single().Lines.Single();

        line.FontSizePoints.ShouldBe(18);
        line.Spans.Single().Font.Slant.ShouldBe(FontSlant.Italic);
    }

    [Fact]
    public void Layout_repaginates_after_style_definition_change()
    {
        var text = string.Join(' ', Enumerable.Repeat("word", 400));
        var style = new ParagraphStyle(
            "Body",
            "Body",
            ParagraphProperties.Default,
            RunProperties.Default with { FontSizePoints = 11 });
        var sheet = StyleSheet.Default with
        {
            ParagraphStyles = StyleSheet.Default.ParagraphStyles.Add(style),
        };
        var paragraph = CreateParagraph(text, "Body");
        var document = CreateDocument(paragraph, sheet);

        var before = new LayoutEngine().Layout(document);
        var editedStyle = style with
        {
            CharacterProperties = RunProperties.Default with { FontSizePoints = 24 },
        };
        var edited = document with
        {
            Styles = sheet with
            {
                ParagraphStyles = sheet.ParagraphStyles.Select(candidate =>
                    candidate.Id == editedStyle.Id ? editedStyle : candidate).ToImmutableArray(),
            },
        };
        var after = new LayoutEngine().Layout(edited);

        after.Pages.Length.ShouldBeGreaterThan(before.Pages.Length);
    }

    [Fact]
    public void Layout_keeps_direct_run_override_when_style_changes()
    {
        var run = Run.Create(
            "Bold body",
            new RunProperties
            {
                Bold = true,
                Overrides = RunPropertyOverrides.Bold,
            });
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(run),
            new ParagraphProperties { StyleId = "Heading1" });
        var document = CreateDocument(paragraph, StyleSheet.Default);

        var layout = new LayoutEngine().Layout(document);
        var span = layout.Pages[0].Paragraphs.Single().Lines.Single().Spans.Single();

        span.Font.Weight.ShouldBe(FontWeight.Bold);
        span.FontSizePoints.ShouldBe(18);
    }

    private static Document CreateDocument(ParagraphBlock paragraph, StyleSheet sheet)
    {
        var section = new Section(Guid.NewGuid(), ImmutableArray.Create<Block>(paragraph));

        return new Document(Guid.NewGuid(), "Styles", ImmutableArray.Create(section), sheet);
    }

    private static ParagraphBlock CreateParagraph(string text, string styleId) =>
        new(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.Create(text)),
            new ParagraphProperties { StyleId = styleId });
}
