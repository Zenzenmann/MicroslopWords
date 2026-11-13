using MicroslopWords.Rendering;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class PageRangeTests
{
    [Fact]
    public void Resolve_all_returns_every_page()
    {
        var result = new PageRange(PageRangeKind.All).Resolve(3);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void Resolve_current_returns_only_the_current_page()
    {
        var result = new PageRange(PageRangeKind.Current, CurrentPage: 2).Resolve(3);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe([2]);
    }

    [Fact]
    public void Resolve_custom_returns_the_inclusive_range()
    {
        var result = new PageRange(
            PageRangeKind.Custom,
            FromPage: 2,
            ToPage: 3).Resolve(3);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe([2, 3]);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(3, 2)]
    [InlineData(1, 4)]
    public void Resolve_rejects_an_out_of_bounds_or_inverted_custom_range(
        int from,
        int to)
    {
        var result = new PageRange(
            PageRangeKind.Custom,
            FromPage: from,
            ToPage: to).Resolve(3);

        result.IsSuccess.ShouldBeFalse();
        result.Diagnostics.Single().Code.ShouldBe(
            RenderingDiagnosticCodes.InvalidPageRange);
    }

    [Fact]
    public void Resolve_rejects_a_current_page_outside_the_document()
    {
        var result = new PageRange(PageRangeKind.Current, CurrentPage: 4).Resolve(3);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Resolve_rejects_an_empty_document()
    {
        var result = new PageRange(PageRangeKind.All).Resolve(0);

        result.IsSuccess.ShouldBeFalse();
    }
}
