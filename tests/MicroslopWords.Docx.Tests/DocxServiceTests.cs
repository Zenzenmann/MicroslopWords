using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using MicroslopWords.Formats.Docx;
using Shouldly;

namespace MicroslopWords.Docx.Tests;

public sealed class DocxServiceTests
{
    [Fact]
    public void SaveAndLoad_round_trips_paragraph_text()
    {
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(Run.Create("Hello world")));
        var section = new Section(
            Guid.NewGuid(),
            ImmutableArray.Create<Block>(paragraph));
        var document = new Document(Guid.NewGuid(), "Test", ImmutableArray.Create(section));

        using var stream = new MemoryStream();
        DocxService.Save(document, stream);
        stream.Position = 0;

        var loaded = DocxService.Load(stream, "Test");

        loaded.Title.ShouldBe("Test");
        loaded.PlainText.ShouldBe("Hello world");
    }
}
