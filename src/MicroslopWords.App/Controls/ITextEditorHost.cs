namespace MicroslopWords.App.Controls;

public interface ITextEditorHost
{
    void InsertText(string text);

    void Backspace();

    void DeleteForward();

    void DeleteWordBackward();

    void DeleteWordForward();

    void SplitParagraph();

    void InsertSoftBreak();

    bool TryMoveTableCell(bool forward);
}
