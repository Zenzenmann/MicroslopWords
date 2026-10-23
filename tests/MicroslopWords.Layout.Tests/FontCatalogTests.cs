using MicroslopWords.Core.Diagnostics;
using Shouldly;

namespace MicroslopWords.Layout.Tests;

public sealed class FontCatalogTests
{
    [Fact]
    public void Empty_catalog_resolves_nothing()
    {
        FontCatalog.Empty.TryResolve(
            new FontKey("Arial", FontWeight.Regular, FontSlant.Upright),
            out _).ShouldBeFalse();
    }

    [Fact]
    public void Create_reports_duplicate_face_as_diagnostic()
    {
        var bytes = TestFonts.TryLoadBytes();

        if (bytes is null)
        {
            return;
        }

        using var first = new FontFace("Arial", FontWeight.Regular, FontSlant.Upright, bytes);
        using var second = new FontFace("Arial", FontWeight.Regular, FontSlant.Upright, bytes);

        var catalog = FontCatalog.Create([first, second]);

        catalog.Diagnostics.ShouldContain(diagnostic =>
            diagnostic.Code == LayoutDiagnosticCodes.FontUnreadable);
    }

    [Fact]
    public void TryResolve_prefers_exact_key_over_fallback()
    {
        var bytes = TestFonts.TryLoadBytes();

        if (bytes is null)
        {
            return;
        }

        using var regular = new FontFace("Alpha", FontWeight.Regular, FontSlant.Upright, bytes);
        using var bold = new FontFace("Alpha", FontWeight.Bold, FontSlant.Upright, bytes);

        var catalog = FontCatalog.Create([regular, bold]);

        catalog.TryResolve(
            new FontKey("Alpha", FontWeight.Bold, FontSlant.Upright),
            out var resolved).ShouldBeTrue();

        resolved.Weight.ShouldBe(FontWeight.Bold);
    }

    [Fact]
    public void TryResolve_falls_back_to_regular_weight_when_bold_missing()
    {
        var bytes = TestFonts.TryLoadBytes();

        if (bytes is null)
        {
            return;
        }

        using var regular = new FontFace("Alpha", FontWeight.Regular, FontSlant.Upright, bytes);
        var catalog = FontCatalog.Create([regular]);

        catalog.TryResolve(
            new FontKey("Alpha", FontWeight.Bold, FontSlant.Upright),
            out var resolved).ShouldBeTrue();

        resolved.Weight.ShouldBe(FontWeight.Regular);
    }

    [Fact]
    public void TryResolve_uses_fallback_family_when_requested_family_absent()
    {
        var bytes = TestFonts.TryLoadBytes();

        if (bytes is null)
        {
            return;
        }

        using var fallback = new FontFace("Fallback", FontWeight.Regular, FontSlant.Upright, bytes);
        var catalog = FontCatalog.Create([fallback], fallbackFamilies: ["Fallback"]);

        catalog.TryResolve(
            new FontKey("Missing", FontWeight.Regular, FontSlant.Upright),
            out var resolved).ShouldBeTrue();

        resolved.Family.ShouldBe("Fallback");
    }

    [Fact]
    public void Catalog_diagnostics_are_exposed_for_the_layout_result()
    {
        var catalog = FontCatalog.Create(
            [],
            diagnostics:
            [
                new Diagnostic(
                    LayoutDiagnosticCodes.FontMissingGlyph,
                    DiagnosticSeverity.Warning,
                    "seed"),
            ]);

        catalog.Diagnostics.ShouldContain(diagnostic =>
            diagnostic.Code == LayoutDiagnosticCodes.FontMissingGlyph);
    }
}
