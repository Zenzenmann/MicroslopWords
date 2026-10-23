using MicroslopWords.Core.Diagnostics;
using Shouldly;

namespace MicroslopWords.Layout.Tests;

public sealed class TextShaperTests
{
    [Fact]
    public void Shape_empty_text_returns_no_clusters()
    {
        using var shaper = new TextShaper(FontCatalog.Empty);

        var shaped = shaper.Shape(string.Empty, new FontKey("Arial"), 11);

        shaped.Clusters.ShouldBeEmpty();
        shaped.AdvanceWidthPoints.ShouldBe(0);
        shaped.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Shape_without_catalog_emits_substitution_diagnostic_and_deterministic_advance()
    {
        using var shaper = new TextShaper(FontCatalog.Empty);
        var request = new FontKey("Arial", FontWeight.Regular, FontSlant.Upright);

        var first = shaper.Shape("abc", request, 12);
        var second = shaper.Shape("abc", request, 12);

        first.Diagnostics.ShouldContain(diagnostic =>
            diagnostic.Code == LayoutDiagnosticCodes.FontSubstituted);
        first.AdvanceWidthPoints.ShouldBeGreaterThan(0);
        first.AdvanceWidthPoints.ShouldBe(second.AdvanceWidthPoints);
        first.Clusters.Length.ShouldBe(3);
        first.Clusters[0].TextStart.ShouldBe(0);
        first.Clusters[1].TextStart.ShouldBe(1);
        first.Clusters[2].TextStart.ShouldBe(2);
    }

    [Fact]
    public void Shape_fallback_records_utf16_offsets_for_astral_runes()
    {
        using var shaper = new TextShaper(FontCatalog.Empty);
        var text = "a\U0001F600b";

        var shaped = shaper.Shape(text, new FontKey("Arial"), 12);

        shaped.Clusters.Length.ShouldBe(3);
        shaped.Clusters[0].TextStart.ShouldBe(0);
        shaped.Clusters[0].TextLength.ShouldBe(1);
        shaped.Clusters[1].TextStart.ShouldBe(1);
        shaped.Clusters[1].TextLength.ShouldBe(2);
        shaped.Clusters[2].TextStart.ShouldBe(3);
        shaped.Clusters[2].TextLength.ShouldBe(1);
    }

    [Fact]
    public void Shape_with_font_produces_positive_advances_and_clusters()
    {
        var bytes = TestFonts.TryLoadBytes();

        if (bytes is null)
        {
            return;
        }

        using var face = new FontFace("Test", FontWeight.Regular, FontSlant.Upright, bytes);
        var catalog = FontCatalog.Create([face]);
        using var shaper = new TextShaper(catalog);

        var shaped = shaper.Shape("Hi", new FontKey("Test"), 16);

        shaped.ResolvedFont.ShouldBe(face.Key);
        shaped.AdvanceWidthPoints.ShouldBeGreaterThan(0);
        shaped.Clusters.ShouldNotBeEmpty();
        shaped.Clusters.ShouldAllBe(cluster => cluster.Advance > 0);
        shaped.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Shape_is_deterministic_for_the_same_input()
    {
        var bytes = TestFonts.TryLoadBytes();

        if (bytes is null)
        {
            return;
        }

        using var face = new FontFace("Test", FontWeight.Regular, FontSlant.Upright, bytes);
        var catalog = FontCatalog.Create([face]);
        using var shaper = new TextShaper(catalog);

        var first = shaper.Shape("Deterministic", new FontKey("Test"), 11);
        var second = shaper.Shape("Deterministic", new FontKey("Test"), 11);

        first.AdvanceWidthPoints.ShouldBe(second.AdvanceWidthPoints);
        first.Clusters.Length.ShouldBe(second.Clusters.Length);

        for (var index = 0; index < first.Clusters.Length; index++)
        {
            first.Clusters[index].ShouldBe(second.Clusters[index]);
        }
    }

    [Fact]
    public void Shape_reports_missing_glyph_once_for_unsupported_character()
    {
        var bytes = TestFonts.TryLoadBytes();

        if (bytes is null)
        {
            return;
        }

        using var face = new FontFace("Test", FontWeight.Regular, FontSlant.Upright, bytes);
        var catalog = FontCatalog.Create([face]);
        using var shaper = new TextShaper(catalog);

        var shaped = shaper.Shape("\uE000\uE001", new FontKey("Test"), 11);

        shaped.Diagnostics.Count(diagnostic =>
            diagnostic.Code == LayoutDiagnosticCodes.FontMissingGlyph).ShouldBe(1);
    }
}
