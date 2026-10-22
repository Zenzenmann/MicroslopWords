using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using MicroslopWords.Layout;
using MicroslopWords.Rendering;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class DocumentRendererTests
{
    [Fact]
    public void RenderPagePng_returns_png_for_layout_page()
    {
        var document = new Document(
            Guid.NewGuid(),
            "Render",
            ImmutableArray.Create(new Section(
                Guid.NewGuid(),
                ImmutableArray.Create<Block>(ParagraphBlock.Create("Hello world")))));
        var layout = new LayoutEngine().Layout(document);

        var png = new DocumentRenderer().RenderPagePng(layout.Pages[0]);

        png.ShouldNotBeEmpty();
        png[0].ShouldBe((byte)0x89);
        png[1].ShouldBe((byte)0x50);
        png[2].ShouldBe((byte)0x4E);
        png[3].ShouldBe((byte)0x47);
    }
}
