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

    void ResizeImage(
        Guid paragraphId,
        Guid runId,
        double widthPoints,
        double heightPoints,
        bool lockAspectRatio);
}
