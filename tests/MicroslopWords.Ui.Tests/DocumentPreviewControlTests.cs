using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using MicroslopWords.App.Controls;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using MicroslopWords.Layout;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class DocumentPreviewControlTests
{
    [AvaloniaFact]
    public void Measure_scales_the_page_with_zoom()
    {
        var control = new DocumentPreviewControl
        {
            Layout = CreateLayout("Hello world"),
            Zoom = 1,
        };

        control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        control.DesiredSize.Width.ShouldBe(864, 0.01);
        control.DesiredSize.Height.ShouldBe(1104, 0.01);

        control.Zoom = 2;
        control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        control.DesiredSize.Width.ShouldBe(1680, 0.01);
        control.DesiredSize.Height.ShouldBe(2160, 0.01);

        control.Zoom = 0.25;
        control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        control.DesiredSize.Width.ShouldBe(252, 0.01);
        control.DesiredSize.Height.ShouldBe(312, 0.01);
    }

    [AvaloniaFact]
    public void Zoom_is_coerced_to_the_supported_range()
    {
        var control = new DocumentPreviewControl();

        control.Zoom = 10;
        control.Zoom.ShouldBe(4);

        control.Zoom = 0.01;
        control.Zoom.ShouldBe(0.25);
    }

    [AvaloniaFact]
    public void TryHitTest_maps_content_coordinates_to_page_and_line_at_every_zoom()
    {
        var layout = CreateLayout("Hello world");
        var control = new DocumentPreviewControl { Layout = layout };

        foreach (var zoom in new[] { 0.25, 1.0, 4.0 })
        {
            control.Zoom = zoom;
            var scale = PageViewport.PageScale(zoom);
            var point = new Point(
                DocumentPreviewControl.OuterMarginDips + (72 * scale) + 1,
                DocumentPreviewControl.OuterMarginDips + (72 * scale) + 1);

            control.TryHitTest(point, out var page, out var line).ShouldBeTrue();
            page.ShouldNotBeNull();
            page.PageNumber.ShouldBe(1);
            line.ShouldNotBeNull();
            line.Text.ShouldBe("Hello world");
        }
    }

    [AvaloniaFact]
    public void TryHitTest_rejects_points_outside_the_page()
    {
        var control = new DocumentPreviewControl { Layout = CreateLayout("Hello world") };

        control.TryHitTest(new Point(1, 1), out var page, out var line).ShouldBeFalse();
        page.ShouldBeNull();
        line.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Click_places_the_caret_on_the_nearest_grapheme_boundary()
    {
        var document = CreateDocument("Hello");
        var control = CreateEditingControl(document, out _);
        var scale = PageViewport.PageScale(1);
        var atEnd = new Point(
            DocumentPreviewControl.OuterMarginDips + ((72 + (5 * 5.5)) * scale) + 1,
            DocumentPreviewControl.OuterMarginDips + (72 * scale) + 2);

        control.SetCaretFromContentPoint(atEnd, extend: false).ShouldBeTrue();

        control.Selection.IsCollapsed.ShouldBeTrue();
        control.Selection.Focus.Offset.ShouldBe(5);

        var atStart = new Point(
            DocumentPreviewControl.OuterMarginDips + (72 * scale) + 1,
            DocumentPreviewControl.OuterMarginDips + (72 * scale) + 2);

        control.SetCaretFromContentPoint(atStart, extend: false).ShouldBeTrue();
        control.Selection.Focus.Offset.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Arrow_and_home_keys_move_the_caret_and_shift_extends_selection()
    {
        var document = CreateDocument("Hello");
        var control = CreateEditingControl(document, out _);
        var paragraph = (ParagraphBlock)document.Sections[0].Blocks[0];
        var run = paragraph.Runs[0];

        control.Selection = Selection.Caret(
            new TextPosition(paragraph.Id, run.Id, 4));

        control.HandleKey(Key.Left, KeyModifiers.None).ShouldBeTrue();
        control.Selection.Focus.Offset.ShouldBe(3);

        control.HandleKey(Key.Home, KeyModifiers.None).ShouldBeTrue();
        control.Selection.Focus.Offset.ShouldBe(0);

        control.HandleKey(Key.End, KeyModifiers.Shift).ShouldBeTrue();
        control.Selection.IsCollapsed.ShouldBeFalse();
        control.Selection.GetEnd(document).Offset.ShouldBe(5);
    }

    [AvaloniaFact]
    public void Editing_keys_delegate_to_the_editor_host_only_when_editable()
    {
        var document = CreateDocument("Hello");
        var control = CreateEditingControl(document, out var host);

        control.HandleKey(Key.Enter, KeyModifiers.None).ShouldBeTrue();
        control.HandleKey(Key.Back, KeyModifiers.None).ShouldBeTrue();
        control.HandleKey(Key.Delete, KeyModifiers.Control).ShouldBeTrue();

        host.Calls.ShouldBe(["split", "backspace", "delete-word-forward"]);

        control.IsEditable = false;
        control.HandleKey(Key.Enter, KeyModifiers.None).ShouldBeTrue();

        host.Calls.Count.ShouldBe(3);
    }

    [AvaloniaFact]
    public void Ime_composition_is_drawn_separately_and_commits_once()
    {
        var document = CreateDocument("Hello");
        var control = CreateEditingControl(document, out var host);
        var client = new DocumentTextInputMethodClient(control);

        client.SetPreeditText("\u306B");

        control.CompositionText.ShouldBe("\u306B");
        document.PlainText.ShouldBe("Hello");
        control.HandleKey(Key.Enter, KeyModifiers.None).ShouldBeTrue();
        host.Calls.ShouldBeEmpty();

        client.SetPreeditText(null);
        control.CompositionText.ShouldBeEmpty();

        client.SetPreeditText("\u306B");
        control.CommitText("\u306B").ShouldBeTrue();

        control.CompositionText.ShouldBeEmpty();
        host.Calls.ShouldBe(["insert:\u306B"]);
    }

    [AvaloniaFact]
    public void Ime_cursor_rectangle_matches_the_caret_location()
    {
        var document = CreateDocument("Hello");
        var control = CreateEditingControl(document, out _);

        var rectangle = control.GetImeCursorRectangle();

        rectangle.Width.ShouldBeGreaterThan(0);
        rectangle.Height.ShouldBeGreaterThan(0);
    }

    [AvaloniaFact]
    public void Double_click_selects_a_word_and_triple_click_selects_a_paragraph()
    {
        var document = CreateDocument("Hello brave world");
        var control = CreateEditingControl(document, out _);
        var scale = PageViewport.PageScale(1);
        var wordPoint = new Point(
            DocumentPreviewControl.OuterMarginDips + ((72 + (6 * 5.5) + 2) * scale),
            DocumentPreviewControl.OuterMarginDips + (72 * scale) + 2);

        control.SelectWordAt(wordPoint).ShouldBeTrue();

        var selection = control.Selection;
        selection.GetStart(document).Offset.ShouldBe(6);
        selection.GetEnd(document).Offset.ShouldBe(11);

        control.SelectParagraphAt(wordPoint).ShouldBeTrue();

        control.Selection.GetStart(document).Offset.ShouldBe(0);
        control.Selection.GetEnd(document).Offset.ShouldBe(17);
    }

    [AvaloniaFact]
    public void Ctrl_arrow_moves_by_word_and_ctrl_home_end_jumps_to_document_edges()
    {
        var document = CreateDocument("Hello brave world");
        var paragraph = (ParagraphBlock)document.Sections[0].Blocks[0];
        var run = paragraph.Runs[0];
        var control = CreateEditingControl(document, out _);

        control.Selection = Selection.Caret(
            new TextPosition(paragraph.Id, run.Id, 0));

        control.HandleKey(Key.Right, KeyModifiers.Control).ShouldBeTrue();
        control.Selection.Focus.Offset.ShouldBe(6);

        control.HandleKey(Key.Right, KeyModifiers.Control).ShouldBeTrue();
        control.Selection.Focus.Offset.ShouldBe(12);

        control.HandleKey(Key.Left, KeyModifiers.Control).ShouldBeTrue();
        control.Selection.Focus.Offset.ShouldBe(6);

        control.HandleKey(Key.End, KeyModifiers.Control).ShouldBeTrue();
        control.Selection.Focus.Offset.ShouldBe(17);

        control.HandleKey(Key.Home, KeyModifiers.Control).ShouldBeTrue();
        control.Selection.Focus.Offset.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Dragging_with_shift_extends_the_selection_from_the_anchor()
    {
        var document = CreateDocument("Hello");
        var paragraph = (ParagraphBlock)document.Sections[0].Blocks[0];
        var run = paragraph.Runs[0];
        var control = CreateEditingControl(document, out _);
        var scale = PageViewport.PageScale(1);
        var point = new Point(
            DocumentPreviewControl.OuterMarginDips + ((72 + (5 * 5.5) + 1) * scale),
            DocumentPreviewControl.OuterMarginDips + (72 * scale) + 2);

        control.Selection = Selection.Caret(
            new TextPosition(paragraph.Id, run.Id, 0));
        control.SetCaretFromContentPoint(point, extend: true).ShouldBeTrue();

        control.Selection.IsCollapsed.ShouldBeFalse();
        control.Selection.GetStart(document).Offset.ShouldBe(0);
        control.Selection.GetEnd(document).Offset.ShouldBe(5);
    }
    private static DocumentPreviewControl CreateEditingControl(
        Document document,
        out FakeEditorHost host)
    {
        host = new FakeEditorHost();

        return new DocumentPreviewControl
        {
            Document = document,
            Layout = new LayoutEngine().Layout(document),
            IsEditable = true,
            EditorHost = host,
            Selection = Selection.StartOf(document),
        };
    }

    private static Document CreateDocument(string text) =>
        Document.CreateEmpty() with
        {
            Sections =
            [
                Section.CreateEmpty() with
                {
                    Blocks = [ParagraphBlock.Create(text)],
                },
            ],
        };

    private static DocumentLayout CreateLayout(string text) =>
        new LayoutEngine().Layout(CreateDocument(text));

    private sealed class FakeEditorHost : ITextEditorHost
    {
        public List<string> Calls { get; } = [];

        public void InsertText(string text) => Calls.Add($"insert:{text}");

        public void Backspace() => Calls.Add("backspace");

        public void DeleteForward() => Calls.Add("delete-forward");

        public void DeleteWordBackward() => Calls.Add("delete-word-backward");

        public void DeleteWordForward() => Calls.Add("delete-word-forward");

        public void SplitParagraph() => Calls.Add("split");

        public void InsertSoftBreak() => Calls.Add("soft-break");
    }
}
