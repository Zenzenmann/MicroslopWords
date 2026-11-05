using Avalonia;
using Avalonia.Input.TextInput;

namespace MicroslopWords.App.Controls;

internal sealed class DocumentTextInputMethodClient : TextInputMethodClient
{
    private readonly DocumentPreviewControl _owner;

    public DocumentTextInputMethodClient(DocumentPreviewControl owner)
    {
        _owner = owner;
    }

    public override Visual TextViewVisual => _owner;

    public override bool SupportsPreedit => true;

    public override bool SupportsSurroundingText => false;

    public override string SurroundingText => string.Empty;

    public override Rect CursorRectangle => _owner.GetImeCursorRectangle();

    public override TextSelection Selection { get; set; }

    public override void SetPreeditText(string? preeditText) =>
        _owner.SetCompositionText(preeditText);

    public override void SetPreeditText(string? preeditText, int? cursorOffset) =>
        _owner.SetCompositionText(preeditText);
}
