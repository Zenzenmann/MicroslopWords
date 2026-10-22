using MicroslopWords.Core.Documents;
using Shouldly;

namespace MicroslopWords.Core.Tests;

public sealed class DocumentTests
{
    [Fact]
    public void CreateEmpty_returns_single_blank_paragraph()
    {
        var document = Document.CreateEmpty("Draft");

        document.Title.ShouldBe("Draft");
        document.Sections.Length.ShouldBe(1);
        document.Sections[0].Blocks.Length.ShouldBe(1);
        document.PlainText.ShouldBeEmpty();
    }
}
