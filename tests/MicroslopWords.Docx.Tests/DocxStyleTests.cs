using System.Collections.Immutable;
using DocumentFormat.OpenXml.Packaging;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Styles;
using MicroslopWords.Formats.Docx;
using Shouldly;
using WpStyle = DocumentFormat.OpenXml.Wordprocessing.Style;

namespace MicroslopWords.Docx.Tests;

public sealed class DocxStyleTests
{
    [Fact]
    public void SaveAndLoad_round_trips_named_style_definitions_and_assignments()
    {
        var paragraphStyle = new ParagraphStyle(
            "UserHeading",
            "Project heading",
            ParagraphProperties.Default with
            {
                Alignment = ParagraphAlignment.Center,
                SpaceBeforePoints = 24,
            },
            RunProperties.Default with { FontSizePoints = 16, Bold = true });
        var characterStyle = new CharacterStyle(
            "UserEmphasis",
            "Project emphasis",
            RunProperties.Default with { Italic = true, ColorHex = "#112233" });
        var sheet = StyleSheet.Default with
        {
            ParagraphStyles = StyleSheet.Default.ParagraphStyles.Add(paragraphStyle),
            CharacterStyles = StyleSheet.Default.CharacterStyles.Add(characterStyle),
        };
        var run = Run.Create(
            "Styled text",
            new RunProperties
            {
                StyleId = "UserEmphasis",
                Bold = true,
                Overrides = RunPropertyOverrides.Bold,
            });
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(run),
            new ParagraphProperties { StyleId = "UserHeading" });
        var section = new Section(Guid.NewGuid(), ImmutableArray.Create<Block>(paragraph));
        var document = new Document(
            Guid.NewGuid(),
            "Styles",
            ImmutableArray.Create(section),
            sheet);

        using var stream = new MemoryStream();
        DocxService.Save(document, stream);

        stream.Position = 0;
        using (var package = WordprocessingDocument.Open(stream, false))
        {
            var styles = package.MainDocumentPart?.StyleDefinitionsPart?.Styles;

            styles.ShouldNotBeNull();
            styles!.Elements<WpStyle>().Any(style =>
                style.StyleId?.Value == "UserHeading").ShouldBeTrue();
            styles.Elements<WpStyle>().Any(style =>
                style.StyleId?.Value == "UserEmphasis").ShouldBeTrue();
        }

        stream.Position = 0;
        var loaded = DocxService.Load(stream, "Styles", TestContext.Current.CancellationToken);

        loaded.IsSuccess.ShouldBeTrue();
        var loadedDocument = loaded.Value!;
        var loadedParagraphStyle = loadedDocument.EffectiveStyles.FindParagraphStyle("UserHeading");
        var loadedCharacterStyle = loadedDocument.EffectiveStyles.FindCharacterStyle("UserEmphasis");

        loadedParagraphStyle.ShouldNotBeNull();
        loadedParagraphStyle!.Name.ShouldBe("Project heading");
        loadedParagraphStyle.Properties.SpaceBeforePoints.ShouldBe(24);
        loadedCharacterStyle.ShouldNotBeNull();
        loadedCharacterStyle!.Properties.Italic.ShouldBeTrue();
        loadedCharacterStyle.Properties.ColorHex.ShouldBe("#112233");

        var loadedParagraph = loadedDocument.Sections.Single()
            .Blocks.OfType<ParagraphBlock>()
            .Single();

        loadedParagraph.Properties!.StyleId.ShouldBe("UserHeading");
        loadedParagraph.Runs.Single().Properties!.StyleId.ShouldBe("UserEmphasis");
        loadedParagraph.Runs.Single().Properties!.Bold.ShouldBeTrue();
        loadedParagraph.Runs.Single().Properties!.Overrides
            .HasFlag(RunPropertyOverrides.Bold).ShouldBeTrue();

        var resolved = StyleResolver.ResolveRun(
            loadedParagraph.Runs.Single(),
            loadedParagraph,
            loadedDocument.EffectiveStyles);

        resolved.FontSizePoints.ShouldBe(16);
        resolved.Italic.ShouldBeTrue();
        resolved.Bold.ShouldBeTrue();
    }

    [Fact]
    public void SaveAndLoad_preserves_builtin_style_definitions()
    {
        var paragraph = ParagraphBlock.Create("Heading");
        var section = new Section(Guid.NewGuid(), ImmutableArray.Create<Block>(paragraph));
        var document = new Document(Guid.NewGuid(), "Builtins", ImmutableArray.Create(section));

        using var stream = new MemoryStream();
        DocxService.Save(document, stream);
        stream.Position = 0;

        var loaded = DocxService.Load(stream, "Builtins", TestContext.Current.CancellationToken);
        var styles = loaded.Value!.EffectiveStyles;

        styles.FindParagraphStyle("Title").ShouldNotBeNull();
        styles.FindParagraphStyle("Heading1")!.CharacterProperties!.FontSizePoints.ShouldBe(18);
        styles.FindParagraphStyle("Heading2").ShouldNotBeNull();
        styles.FindParagraphStyle("Heading3").ShouldNotBeNull();
        styles.FindCharacterStyle(StyleSheet.DefaultCharacterStyleId).ShouldNotBeNull();
    }
}
