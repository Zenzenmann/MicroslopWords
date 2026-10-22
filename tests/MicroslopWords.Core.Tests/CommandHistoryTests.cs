using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using Shouldly;

namespace MicroslopWords.Core.Tests;

public sealed class CommandHistoryTests
{
    [Fact]
    public void Execute_undo_redo_restores_text()
    {
        var document = Document.CreateEmpty();
        var paragraph = (ParagraphBlock)document.Sections[0].Blocks[0];
        var history = new CommandHistory(document);
        var command = new InsertTextCommand(paragraph.Id, Run.Create("Hello"));

        history.Execute(command);
        history.Current.PlainText.ShouldBe("Hello");

        history.Undo();
        history.Current.PlainText.ShouldBeEmpty();

        history.Redo();
        history.Current.PlainText.ShouldBe("Hello");
        history.CanUndo.ShouldBeTrue();
        history.CanRedo.ShouldBeFalse();
    }
}
