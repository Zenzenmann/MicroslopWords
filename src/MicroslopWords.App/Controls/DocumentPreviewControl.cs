using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using MicroslopWords.Layout;

namespace MicroslopWords.App.Controls;

public sealed class DocumentPreviewControl : Control
{
    public const double OuterMarginDips = PageViewport.OuterMarginDips;
    public const double PageGapDips = PageViewport.PageGapDips;

    public static readonly StyledProperty<DocumentLayout?> LayoutProperty =
        AvaloniaProperty.Register<DocumentPreviewControl, DocumentLayout?>(nameof(Layout));

    public static readonly StyledProperty<Document?> DocumentProperty =
        AvaloniaProperty.Register<DocumentPreviewControl, Document?>(nameof(Document));

    public static readonly StyledProperty<Selection> SelectionProperty =
        AvaloniaProperty.Register<DocumentPreviewControl, Selection>(
            nameof(Selection),
            default,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsEditableProperty =
        AvaloniaProperty.Register<DocumentPreviewControl, bool>(nameof(IsEditable));

    public static readonly StyledProperty<ITextEditorHost?> EditorHostProperty =
        AvaloniaProperty.Register<DocumentPreviewControl, ITextEditorHost?>(nameof(EditorHost));

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<DocumentPreviewControl, double>(
            nameof(Zoom),
            1.0,
            coerce: CoerceZoom);

    public static readonly StyledProperty<int> CurrentPageProperty =
        AvaloniaProperty.Register<DocumentPreviewControl, int>(nameof(CurrentPage), 1);

    private static readonly IBrush PageBrush =
        new SolidColorBrush(Color.Parse("#FFFEFB"));

    private static readonly IBrush PageNumberBrush =
        new SolidColorBrush(Color.Parse("#5F6A76"));

    private static readonly IBrush TextBrush =
        new SolidColorBrush(Color.Parse("#1B1F24"));

    private static readonly IBrush SelectionBrush =
        new SolidColorBrush(Color.Parse("#CFE3F5"));

    private static readonly IBrush CaretBrush =
        new SolidColorBrush(Color.Parse("#1B1F24"));

    private static readonly Pen PageBorder =
        new(new SolidColorBrush(Color.Parse("#C9C9C9")), 1);

    private static readonly Pen CompositionUnderline =
        new(new SolidColorBrush(Color.Parse("#1B1F24")), 1);

    private readonly DocumentTextInputMethodClient _imeClient;
    private ScrollViewer? _scrollViewer;
    private Vector? _pendingScrollOffset;
    private double? _pointerAnchorX;
    private double? _pointerAnchorY;
    private string _compositionText = string.Empty;
    private bool _isSelecting;

    public DocumentPreviewControl()
    {
        Focusable = true;
        _imeClient = new DocumentTextInputMethodClient(this);
        TextInputMethodClientRequested += OnTextInputMethodClientRequested;
    }

    public DocumentLayout? Layout
    {
        get => GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    public Document? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public Selection Selection
    {
        get => GetValue(SelectionProperty);
        set => SetValue(SelectionProperty, value);
    }

    public bool IsEditable
    {
        get => GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    public ITextEditorHost? EditorHost
    {
        get => GetValue(EditorHostProperty);
        set => SetValue(EditorHostProperty, value);
    }

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public int CurrentPage
    {
        get => GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    internal string CompositionText => _compositionText;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _scrollViewer = this.FindAncestorOfType<ScrollViewer>();

        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged += OnScrollChanged;
            _scrollViewer.SizeChanged += OnScrollViewerSizeChanged;
        }

        LayoutUpdated += OnLayoutUpdated;
        RequestEnsureCaretVisible();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdated;

        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged -= OnScrollChanged;
            _scrollViewer.SizeChanged -= OnScrollViewerSizeChanged;
            _scrollViewer = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LayoutProperty)
        {
            InvalidateMeasure();
            InvalidateVisual();
            UpdateCurrentPage();
            RequestEnsureCaretVisible();
        }
        else if (change.Property == DocumentProperty)
        {
            InvalidateVisual();
            RequestEnsureCaretVisible();
        }
        else if (change.Property == SelectionProperty)
        {
            InvalidateVisual();
            RequestEnsureCaretVisible();
        }
        else if (change.Property == IsEditableProperty && !IsEditable)
        {
            _compositionText = string.Empty;
            InvalidateVisual();
        }
        else if (change.Property == ZoomProperty)
        {
            var oldZoom = change.GetOldValue<double>();
            var newZoom = change.GetNewValue<double>();

            if (!AreClose(oldZoom, newZoom))
            {
                AdjustScrollForZoom(oldZoom, newZoom);
            }

            InvalidateMeasure();
            InvalidateVisual();
            UpdateCurrentPage();
            RequestEnsureCaretVisible();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Layout is null)
        {
            return default;
        }

        var scale = PageViewport.PageScale(Zoom);
        var widestPage = Layout.Pages.Length == 0
            ? Layout.PageWidthPoints
            : Layout.Pages.Max(page => page.Bounds.Width);
        var width = (widestPage * scale) + (OuterMarginDips * 2);
        var height = Layout.Pages.Sum(page => page.Bounds.Height * scale)
            + (PageGapDips * Math.Max(0, Layout.Pages.Length - 1))
            + (OuterMarginDips * 2);

        return new Size(width, height);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var layout = Layout;

        if (layout is null)
        {
            return;
        }

        var scale = PageViewport.PageScale(Zoom);
        var widestPage = (layout.Pages.Length == 0
                ? layout.PageWidthPoints
                : layout.Pages.Max(page => page.Bounds.Width)) * scale;
        var y = OuterMarginDips;

        foreach (var page in layout.Pages)
        {
            var pageWidth = page.Bounds.Width * scale;
            var pageHeight = page.Bounds.Height * scale;
            var pageRect = new Rect(
                OuterMarginDips + ((widestPage - pageWidth) / 2),
                y,
                pageWidth,
                pageHeight);

            context.FillRectangle(PageBrush, pageRect);

            using (context.PushClip(pageRect))
            {
                DrawSelection(context, page, pageRect, scale);

                foreach (var line in page.Paragraphs.SelectMany(paragraph => paragraph.Lines))
                {
                    DrawLine(context, line, pageRect, scale);
                }
            }

            context.DrawRectangle(null, PageBorder, pageRect);
            DrawPageNumber(context, page.PageNumber, pageRect);

            y += pageHeight + PageGapDips;
        }

        DrawCaret(context, scale);
    }

    public bool TryHitTest(Point contentPoint, out PageLayout? page, out LineLayout? line)
    {
        page = null;
        line = null;

        if (!TryGetPageAt(contentPoint, out var hitPage, out var pageRect, out _)
            || Layout is null)
        {
            return false;
        }

        var scale = PageViewport.PageScale(Zoom);
        var pagePointX = ((contentPoint.X - pageRect.X) / scale) + hitPage.Bounds.X;
        var pagePointY = ((contentPoint.Y - pageRect.Y) / scale) + hitPage.Bounds.Y;
        var hit = LayoutHitTesting.HitTestLine(
            Layout,
            hitPage.PageNumber,
            pagePointX,
            pagePointY);

        page = hitPage;

        if (hit is null)
        {
            return true;
        }

        line = hit.Value.Line;
        return true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus();

        if (Document is null || Layout is null
            || !TryGetTextPosition(e.GetPosition(this), out var position))
        {
            return;
        }

        if (e.ClickCount >= 3)
        {
            Selection = TextNavigation.GetParagraphSelection(Document, position);
        }
        else if (e.ClickCount == 2)
        {
            Selection = TextNavigation.GetWordSelection(Document, position);
        }
        else
        {
            SetCaret(position, (e.KeyModifiers & KeyModifiers.Shift) != 0);
            _isSelecting = true;
            e.Pointer.Capture(this);
        }

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_isSelecting
            && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            SetCaretFromContentPoint(e.GetPosition(this), extend: true);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_isSelecting)
        {
            _isSelecting = false;
            e.Pointer.Capture(null);
        }

        base.OnPointerReleased(e);
    }

    internal bool SelectWordAt(Point contentPoint)
    {
        if (Document is null || !TryGetTextPosition(contentPoint, out var position))
        {
            return false;
        }

        Selection = TextNavigation.GetWordSelection(Document, position);
        return true;
    }

    internal bool SelectParagraphAt(Point contentPoint)
    {
        if (Document is null || !TryGetTextPosition(contentPoint, out var position))
        {
            return false;
        }

        Selection = TextNavigation.GetParagraphSelection(Document, position);
        return true;
    }
    internal bool SetCaretFromContentPoint(Point contentPoint, bool extend)
    {
        if (!TryGetTextPosition(contentPoint, out var position))
        {
            return false;
        }

        SetCaret(position, extend);
        return true;
    }

    private bool TryGetTextPosition(Point contentPoint, out TextPosition position)
    {
        position = default;

        if (Document is null
            || Layout is null
            || !TryGetPageAt(contentPoint, out var page, out var pageRect, out _))
        {
            return false;
        }

        var scale = PageViewport.PageScale(Zoom);
        var pageX = ((contentPoint.X - pageRect.X) / scale) + page.Bounds.X;
        var pageY = ((contentPoint.Y - pageRect.Y) / scale) + page.Bounds.Y;
        var hit = LayoutHitTesting.HitTestLine(Layout, page.PageNumber, pageX, pageY);

        if (hit is null)
        {
            return false;
        }

        var paragraph = Document.FindParagraph(hit.Value.Paragraph.ParagraphId);

        if (paragraph is null)
        {
            return false;
        }

        position = TextNavigation.FromParagraphTextOffset(
            paragraph,
            hit.Value.ParagraphOffset);

        return true;
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (HandleKey(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    internal bool HandleKey(Key key, KeyModifiers modifiers)
    {
        if (_compositionText.Length > 0)
        {
            if (key == Key.Escape)
            {
                _compositionText = string.Empty;
                InvalidateVisual();
            }

            return true;
        }

        if (Document is null || Layout is null)
        {
            return false;
        }

        var extend = (modifiers & KeyModifiers.Shift) != 0;
        var command = (modifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        var selection = SelectionMap.Normalize(Selection, Document);

        switch (key)
        {
            case Key.Left:
                SetCaret(
                    command
                        ? TextNavigation.PreviousWord(Document, selection.Focus)
                        : TextNavigation.PreviousGrapheme(Document, selection.Focus),
                    extend);
                return true;
            case Key.Right:
                SetCaret(
                    command
                        ? TextNavigation.NextWord(Document, selection.Focus)
                        : TextNavigation.NextGrapheme(Document, selection.Focus),
                    extend);
                return true;
            case Key.Up:
                MoveLine(-1, extend);
                return true;
            case Key.Down:
                MoveLine(1, extend);
                return true;
            case Key.Home when command:
                SetCaret(Selection.StartOf(Document).Focus, extend);
                return true;
            case Key.Home:
                MoveToLineBoundary(start: true, extend);
                return true;
            case Key.End when command:
                SetCaret(Selection.EndOf(Document).Focus, extend);
                return true;
            case Key.End:
                MoveToLineBoundary(start: false, extend);
                return true;
            case Key.PageUp:
                MoveViewport(-1, extend);
                return true;
            case Key.PageDown:
                MoveViewport(1, extend);
                return true;
            case Key.Back when command:
                if (IsEditable)
                {
                    EditorHost?.DeleteWordBackward();
                }

                return true;
            case Key.Back:
                if (IsEditable)
                {
                    EditorHost?.Backspace();
                }

                return true;
            case Key.Delete when command:
                if (IsEditable)
                {
                    EditorHost?.DeleteWordForward();
                }

                return true;
            case Key.Delete:
                if (IsEditable)
                {
                    EditorHost?.DeleteForward();
                }

                return true;
            case Key.Enter when extend:
                if (IsEditable)
                {
                    EditorHost?.InsertSoftBreak();
                }

                return true;
            case Key.Enter:
                if (IsEditable)
                {
                    EditorHost?.SplitParagraph();
                }

                return true;
            default:
                return false;
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (CommitText(e.Text))
        {
            e.Handled = true;
            return;
        }

        base.OnTextInput(e);
    }

    internal bool CommitText(string? text)
    {
        if (!IsEditable || EditorHost is null || string.IsNullOrEmpty(text))
        {
            return false;
        }

        _compositionText = string.Empty;
        EditorHost.InsertText(text);
        return true;
    }

    internal Rect GetImeCursorRectangle()
    {
        var caret = GetCaretContentRect();

        return caret ?? default;
    }

    internal void SetCompositionText(string? text)
    {
        var value = text ?? string.Empty;

        if (_compositionText == value)
        {
            return;
        }

        _compositionText = IsEditable ? value : string.Empty;
        InvalidateVisual();
    }

    private void OnTextInputMethodClientRequested(
        object? sender,
        TextInputMethodClientRequestedEventArgs e) =>
        e.Client = _imeClient;

    private void SetCaret(TextPosition position, bool extend)
    {
        if (Document is null)
        {
            return;
        }

        var normalized = TextNavigation.Normalize(Document, position);

        if (extend)
        {
            var current = SelectionMap.Normalize(Selection, Document);
            Selection = new Selection(current.Anchor, normalized);
        }
        else
        {
            Selection = Selection.Caret(normalized);
        }
    }

    private void MoveLine(int direction, bool extend)
    {
        if (Document is null || Layout is null)
        {
            return;
        }

        var selection = SelectionMap.Normalize(Selection, Document);
        var focus = selection.Focus;
        var paragraphOffset = GetParagraphOffset(focus);
        var caret = LayoutHitTesting.FindCaret(
            Layout,
            focus.ParagraphId,
            paragraphOffset,
            focus.Affinity);

        if (caret is null)
        {
            return;
        }

        var page = caret.Page;
        var lineIndex = caret.LineIndex + direction;
        var pageIndex = IndexOfPage(page);

        if (lineIndex < 0)
        {
            if (pageIndex <= 0)
            {
                return;
            }

            page = Layout.Pages[pageIndex - 1];
            lineIndex = CountLines(page) - 1;
        }
        else if (lineIndex >= CountLines(page))
        {
            if (pageIndex + 1 >= Layout.Pages.Length)
            {
                return;
            }

            page = Layout.Pages[pageIndex + 1];
            lineIndex = 0;
        }

        var targetLine = LayoutHitTesting.GetLine(page, lineIndex);

        if (targetLine is null)
        {
            return;
        }

        var hit = LayoutHitTesting.HitTestLine(
            Layout,
            page.PageNumber,
            caret.Bounds.X,
            targetLine.Bounds.Y + 1);

        if (hit is null)
        {
            return;
        }

        var paragraph = Document.FindParagraph(hit.Value.Paragraph.ParagraphId);

        if (paragraph is null)
        {
            return;
        }

        SetCaret(
            TextNavigation.FromParagraphTextOffset(
                paragraph,
                hit.Value.ParagraphOffset,
                focus.Affinity),
            extend);
    }

    private void MoveViewport(int direction, bool extend)
    {
        if (Document is null || Layout is null)
        {
            return;
        }

        var caret = GetCaretContentRect();

        if (caret is null)
        {
            return;
        }

        var viewportHeight = _scrollViewer?.Viewport.Height ?? 800;
        var target = new Point(
            caret.Value.X,
            caret.Value.Y + (direction * viewportHeight * 0.9));

        if (SetCaretFromContentPoint(target, extend))
        {
            return;
        }

        SetCaret(
            direction < 0
                ? Selection.StartOf(Document).Focus
                : Selection.EndOf(Document).Focus,
            extend);
    }

    private void MoveToLineBoundary(bool start, bool extend)
    {
        if (Document is null || Layout is null)
        {
            return;
        }

        var selection = SelectionMap.Normalize(Selection, Document);
        var caret = LayoutHitTesting.FindCaret(
            Layout,
            selection.Focus.ParagraphId,
            GetParagraphOffset(selection.Focus),
            selection.Focus.Affinity);

        if (caret is null)
        {
            return;
        }

        var paragraph = Document.FindParagraph(selection.Focus.ParagraphId);

        if (paragraph is null)
        {
            return;
        }

        var offset = start
            ? caret.Line.TextStart
            : caret.Line.TextStart + caret.Line.Text.Length;

        SetCaret(
            TextNavigation.FromParagraphTextOffset(
                paragraph,
                offset,
                start ? TextAffinity.Downstream : TextAffinity.Upstream),
            extend);
    }

    private int GetParagraphOffset(TextPosition position)
    {
        if (Document is null)
        {
            return 0;
        }

        var paragraph = Document.FindParagraph(position.ParagraphId);

        return paragraph is null
            ? 0
            : TextNavigation.GetParagraphTextOffset(
                paragraph,
                position.RunId,
                position.Offset);
    }

    private static void DrawLine(
        DrawingContext context,
        LineLayout line,
        Rect pageRect,
        double scale)
    {
        if (line.Text.Length == 0)
        {
            return;
        }

        if (line.Spans.Length == 0)
        {
            DrawSpan(
                context,
                line.Text,
                line.ResolvedFont,
                line.FontSizePoints,
                line.Bounds.Width,
                "#000000",
                null,
                underline: false,
                strikethrough: false,
                pageRect,
                line,
                xOffset: 0,
                scale);
            return;
        }

        foreach (var span in line.Spans)
        {
            if (span.TextLength <= 0
                || span.TextStart < 0
                || span.TextStart + span.TextLength > line.Text.Length)
            {
                continue;
            }

            DrawSpan(
                context,
                line.Text.Substring(span.TextStart, span.TextLength),
                span.Font,
                span.FontSizePoints,
                span.Width,
                span.ColorHex,
                span.HighlightHex,
                span.Underline,
                span.Strikethrough,
                pageRect,
                line,
                span.X,
                scale);
        }
    }

    private static void DrawSpan(
        DrawingContext context,
        string text,
        FontKey font,
        double fontSizePoints,
        double widthPoints,
        string colorHex,
        string? highlightHex,
        bool underline,
        bool strikethrough,
        Rect pageRect,
        LineLayout line,
        double xOffset,
        double scale)
    {
        if (text.Length == 0)
        {
            return;
        }

        var x = pageRect.X + ((line.Bounds.X + xOffset) * scale);
        var y = pageRect.Y + (line.Bounds.Y * scale);
        var height = line.Bounds.Height * scale;
        var brush = CreateBrush(colorHex);

        if (!string.IsNullOrWhiteSpace(highlightHex))
        {
            context.FillRectangle(
                CreateBrush(highlightHex),
                new Rect(x, y, Math.Max(1, widthPoints * scale), height));
        }

        var formatted = CreateFormattedText(
            text,
            font,
            fontSizePoints * scale);

        context.DrawText(formatted, new Point(x, y));

        var pen = new Pen(brush, Math.Max(1, scale));

        if (underline)
        {
            context.DrawLine(
                pen,
                new Point(x, y + height),
                new Point(x + formatted.Width, y + height));
        }

        if (strikethrough)
        {
            var middle = y + (height / 2);

            context.DrawLine(
                pen,
                new Point(x, middle),
                new Point(x + formatted.Width, middle));
        }
    }

    private static IBrush CreateBrush(string? colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            return TextBrush;
        }

        try
        {
            return new SolidColorBrush(Color.Parse(colorHex));
        }
        catch (Exception exception) when (
            exception is FormatException or ArgumentException)
        {
            return TextBrush;
        }
    }

    private void DrawSelection(
        DrawingContext context,
        PageLayout page,
        Rect pageRect,
        double scale)
    {
        if (Document is null || Layout is null)
        {
            return;
        }

        var selection = SelectionMap.Normalize(Selection, Document);

        if (selection.IsCollapsed)
        {
            return;
        }

        var paragraphs = Document.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .ToList();
        var start = selection.GetStart(Document);
        var end = selection.GetEnd(Document);
        var startIndex = FindParagraphIndex(paragraphs, start.ParagraphId);
        var endIndex = FindParagraphIndex(paragraphs, end.ParagraphId);

        if (startIndex < 0 || endIndex < 0)
        {
            return;
        }

        var startOffset = GetParagraphOffset(start);
        var endOffset = GetParagraphOffset(end);

        foreach (var paragraphLayout in page.Paragraphs)
        {
            var index = FindParagraphIndex(paragraphs, paragraphLayout.ParagraphId);

            if (index < startIndex || index > endIndex)
            {
                continue;
            }

            var paragraph = paragraphs[index];
            var selectionStart = index == startIndex ? startOffset : 0;
            var selectionEnd = index == endIndex
                ? endOffset
                : paragraph.PlainText.Length;

            foreach (var line in paragraphLayout.Lines)
            {
                var lineEnd = line.TextStart + line.Text.Length;
                var from = Math.Max(selectionStart, line.TextStart);
                var to = Math.Min(selectionEnd, lineEnd);

                if (to <= from)
                {
                    continue;
                }

                var x = LayoutHitTesting.GetCaretX(line, from);
                var right = LayoutHitTesting.GetCaretX(line, to);
                var rect = new Rect(
                    pageRect.X + (x * scale),
                    pageRect.Y + (line.Bounds.Y * scale),
                    Math.Max(1, (right - x) * scale),
                    line.Bounds.Height * scale);

                context.FillRectangle(SelectionBrush, rect);
            }
        }
    }

    private void DrawCaret(DrawingContext context, double scale)
    {
        if (Document is null || Layout is null)
        {
            return;
        }

        var caret = GetCaretLayout();

        if (caret is null)
        {
            return;
        }

        var (caretLayout, pageRect) = caret.Value;
        var x = pageRect.X + (caretLayout.Bounds.X * scale);
        var y = pageRect.Y + (caretLayout.Bounds.Y * scale);
        var height = caretLayout.Bounds.Height * scale;

        if (_compositionText.Length > 0 && IsEditable)
        {
            var formatted = CreateFormattedText(
                _compositionText,
                caretLayout.Line.ResolvedFont,
                caretLayout.Line.FontSizePoints * scale);
            var compositionPoint = new Point(x, y);

            context.DrawText(formatted, compositionPoint);
            context.DrawLine(
                CompositionUnderline,
                new Point(x, y + height),
                new Point(x + formatted.Width, y + height));
            x += formatted.Width;
        }

        context.FillRectangle(CaretBrush, new Rect(x, y, 1.5, height));
    }

    private (CaretLayout Caret, Rect PageRect)? GetCaretLayout()
    {
        if (Document is null || Layout is null)
        {
            return null;
        }

        var selection = SelectionMap.Normalize(Selection, Document);
        var focus = selection.Focus;
        var caret = LayoutHitTesting.FindCaret(
            Layout,
            focus.ParagraphId,
            GetParagraphOffset(focus),
            focus.Affinity);

        if (caret is null)
        {
            return null;
        }

        var pageRect = GetPageRect(caret.Page);

        return pageRect is null ? null : (caret, pageRect.Value);
    }

    private Rect? GetCaretContentRect()
    {
        var caret = GetCaretLayout();

        if (caret is null)
        {
            return null;
        }

        var (caretLayout, pageRect) = caret.Value;
        var scale = PageViewport.PageScale(Zoom);

        return new Rect(
            pageRect.X + (caretLayout.Bounds.X * scale),
            pageRect.Y + (caretLayout.Bounds.Y * scale),
            1,
            caretLayout.Bounds.Height * scale);
    }

    private Rect? GetPageRect(PageLayout page)
    {
        var layout = Layout;

        if (layout is null)
        {
            return null;
        }

        var scale = PageViewport.PageScale(Zoom);
        var widestPage = (layout.Pages.Length == 0
                ? layout.PageWidthPoints
                : layout.Pages.Max(candidate => candidate.Bounds.Width)) * scale;
        var y = OuterMarginDips;

        foreach (var candidate in layout.Pages)
        {
            var width = candidate.Bounds.Width * scale;
            var height = candidate.Bounds.Height * scale;
            var rect = new Rect(
                OuterMarginDips + ((widestPage - width) / 2),
                y,
                width,
                height);

            if (candidate.PageNumber == page.PageNumber)
            {
                return rect;
            }

            y += height + PageGapDips;
        }

        return null;
    }

    private bool TryGetPageAt(
        Point contentPoint,
        out PageLayout page,
        out Rect pageRect,
        out int pageIndex)
    {
        page = null!;
        pageRect = default;
        pageIndex = -1;

        var layout = Layout;

        if (layout is null)
        {
            return false;
        }

        var scale = PageViewport.PageScale(Zoom);
        var widestPage = (layout.Pages.Length == 0
                ? layout.PageWidthPoints
                : layout.Pages.Max(candidate => candidate.Bounds.Width)) * scale;
        var y = OuterMarginDips;

        for (var index = 0; index < layout.Pages.Length; index++)
        {
            var candidate = layout.Pages[index];
            var width = candidate.Bounds.Width * scale;
            var height = candidate.Bounds.Height * scale;
            var rect = new Rect(
                OuterMarginDips + ((widestPage - width) / 2),
                y,
                width,
                height);

            if (rect.Contains(contentPoint))
            {
                page = candidate;
                pageRect = rect;
                pageIndex = index;
                return true;
            }

            y += height + PageGapDips;
        }

        return false;
    }

    private int IndexOfPage(PageLayout page)
    {
        var layout = Layout;

        if (layout is null)
        {
            return -1;
        }

        for (var index = 0; index < layout.Pages.Length; index++)
        {
            if (layout.Pages[index].PageNumber == page.PageNumber)
            {
                return index;
            }
        }

        return -1;
    }

    private static int CountLines(PageLayout page) =>
        page.Paragraphs.Sum(paragraph => paragraph.Lines.Length);

    private static int FindParagraphIndex(
        List<ParagraphBlock> paragraphs,
        Guid paragraphId)
    {
        for (var index = 0; index < paragraphs.Count; index++)
        {
            if (paragraphs[index].Id == paragraphId)
            {
                return index;
            }
        }

        return -1;
    }

    private static FormattedText CreateFormattedText(
        string text,
        FontKey font,
        double fontSize) =>
        new(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(
                new FontFamily(font.Family),
                font.Slant == FontSlant.Italic ? FontStyle.Italic : FontStyle.Normal,
                font.Weight == MicroslopWords.Layout.FontWeight.Bold
                    ? Avalonia.Media.FontWeight.Bold
                    : Avalonia.Media.FontWeight.Normal),
            Math.Max(1, fontSize),
            TextBrush);

    private static void DrawPageNumber(
        DrawingContext context,
        int pageNumber,
        Rect pageRect)
    {
        var formatted = new FormattedText(
            $"Page {pageNumber}",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            11,
            PageNumberBrush);

        context.DrawText(formatted, new Point(pageRect.X, pageRect.Y - 18));
    }

    private void AdjustScrollForZoom(double oldZoom, double newZoom)
    {
        if (_scrollViewer is null)
        {
            return;
        }

        var anchorX = _pointerAnchorX
            ?? (_scrollViewer.Offset.X + (_scrollViewer.Viewport.Width / 2));
        var anchorY = _pointerAnchorY
            ?? (_scrollViewer.Offset.Y + (_scrollViewer.Viewport.Height / 2));

        var (offsetX, offsetY) = PageViewport.OffsetForAnchoredZoom(
            oldZoom,
            newZoom,
            anchorX,
            anchorY,
            _scrollViewer.Offset.X,
            _scrollViewer.Offset.Y);

        _pointerAnchorX = null;
        _pointerAnchorY = null;
        _pendingScrollOffset = new Vector(offsetX, offsetY);
        InvalidateMeasure();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_pendingScrollOffset is { } offset && _scrollViewer is not null)
        {
            _pendingScrollOffset = null;
            _scrollViewer.Offset = offset;
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) =>
        UpdateCurrentPage();

    private void OnScrollViewerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateCurrentPage();
        InvalidateMeasure();
    }

    private void UpdateCurrentPage()
    {
        if (_scrollViewer is null || Layout is null || Layout.Pages.Length == 0)
        {
            return;
        }

        var scale = PageViewport.PageScale(Zoom);
        var viewportCenter = _scrollViewer.Offset.Y + (_scrollViewer.Viewport.Height / 2);
        var y = OuterMarginDips;
        var currentPage = Layout.Pages[^1].PageNumber;

        foreach (var page in Layout.Pages)
        {
            var pageHeight = page.Bounds.Height * scale;

            if (viewportCenter < y + pageHeight)
            {
                currentPage = page.PageNumber;
                break;
            }

            y += pageHeight + PageGapDips;
        }

        CurrentPage = currentPage;
    }

    private void RequestEnsureCaretVisible() =>
        Dispatcher.UIThread.Post(EnsureCaretVisibleNow, DispatcherPriority.Background);

    private void EnsureCaretVisibleNow()
    {
        if (_scrollViewer is null)
        {
            return;
        }

        var caret = GetCaretContentRect();

        if (caret is null)
        {
            return;
        }

        var viewport = _scrollViewer.Viewport;

        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        var offset = _scrollViewer.Offset;
        var x = offset.X;
        var y = offset.Y;

        if (caret.Value.X < offset.X)
        {
            x = caret.Value.X;
        }
        else if (caret.Value.Right > offset.X + viewport.Width)
        {
            x = caret.Value.Right - viewport.Width;
        }

        if (caret.Value.Y < offset.Y)
        {
            y = caret.Value.Y;
        }
        else if (caret.Value.Bottom > offset.Y + viewport.Height)
        {
            y = caret.Value.Bottom - viewport.Height;
        }

        if (!AreClose(x, offset.X) || !AreClose(y, offset.Y))
        {
            _scrollViewer.Offset = new Vector(Math.Max(0, x), Math.Max(0, y));
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) == 0)
        {
            base.OnPointerWheelChanged(e);
            return;
        }

        var pointer = e.GetPosition(this);
        var factor = e.Delta.Y >= 0 ? 1.1 : 1 / 1.1;
        var newZoom = PageViewport.ClampZoom(Zoom * factor);

        if (!AreClose(newZoom, Zoom))
        {
            _pointerAnchorX = pointer.X;
            _pointerAnchorY = pointer.Y;
            Zoom = newZoom;
        }

        e.Handled = true;
    }

    private static double CoerceZoom(AvaloniaObject sender, double value) =>
        PageViewport.ClampZoom(value);

    private static bool AreClose(double left, double right) =>
        Math.Abs(left - right) < 0.0001;
}
