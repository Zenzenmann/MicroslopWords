using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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

    public static readonly StyledProperty<IReadOnlyList<Selection>?> SearchHighlightsProperty =
        AvaloniaProperty.Register<DocumentPreviewControl, IReadOnlyList<Selection>?>(
            nameof(SearchHighlights));

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

    private static readonly IBrush SearchHighlightBrush =
        new SolidColorBrush(Color.Parse("#FFF0B3"));

    private static readonly IBrush CaretBrush =
        new SolidColorBrush(Color.Parse("#1B1F24"));

    private static readonly Pen PageBorder =
        new(new SolidColorBrush(Color.Parse("#C9C9C9")), 1);

    private static readonly Pen CompositionUnderline =
        new(new SolidColorBrush(Color.Parse("#1B1F24")), 1);

    private static readonly Pen PageBreakPen =
        new(new SolidColorBrush(Color.Parse("#B26B00")), 1.5);

    private static readonly IBrush PageBreakBrush =
        new SolidColorBrush(Color.Parse("#B26B00"));

    private static readonly IBrush PlaceholderFill =
        new SolidColorBrush(Color.Parse("#F2F2F2"));

    private static readonly Pen PlaceholderBorder =
        new(new SolidColorBrush(Color.Parse("#9A9A9A")), 1);

    private static readonly Pen ImageSelectionPen =
        new(new SolidColorBrush(Color.Parse("#0B5CAD")), 2);

    private static readonly IBrush ImageHandleFill =
        new SolidColorBrush(Colors.White);

    private readonly Dictionary<InlineImage, Bitmap?> _imageCache = [];
    private readonly DocumentTextInputMethodClient _imeClient;
    private ImageResizeState? _imageResize;
    private ImageResizePreview? _imageResizePreview;
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

    public IReadOnlyList<Selection>? SearchHighlights
    {
        get => GetValue(SearchHighlightsProperty);
        set => SetValue(SearchHighlightsProperty, value);
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
        else if (change.Property == SearchHighlightsProperty)
        {
            InvalidateVisual();
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
                DrawTables(context, page, pageRect, scale);
                DrawSearchHighlights(context, page, pageRect, scale);
                DrawSelection(context, page, pageRect, scale);
                DrawListLabels(context, page, pageRect, scale);

                foreach (var line in PageParagraphs(page)
                             .SelectMany(paragraph => paragraph.Lines))
                {
                    DrawLine(context, line, pageRect, scale);
                }

                DrawImageSelection(context, page, pageRect, scale);
                DrawImageResizePreview(context);
                DrawPageBreaks(context, layout, page, pageRect, scale);
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

        if (Document is null || Layout is null)
        {
            return;
        }

        var pointer = e.GetPosition(this);

        if (TryGetImagePlacementAt(
            pointer,
            out var imageParagraphId,
            out var imagePlacement,
            out var imagePage,
            out var imagePageRect))
        {
            if (TryStartImageResize(
                pointer,
                imageParagraphId,
                imagePlacement,
                imagePage,
                imagePageRect))
            {
                e.Pointer.Capture(this);
            }

            Selection = new Selection(
                new TextPosition(imageParagraphId, imagePlacement.RunId, 0),
                new TextPosition(imageParagraphId, imagePlacement.RunId, 1));
            e.Handled = true;
            return;
        }

        if (!TryGetTextPosition(e.GetPosition(this), out var position))
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

        if (_imageResize is { } resize
            && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            UpdateImageResize(resize, e.GetPosition(this));
            e.Handled = true;
            return;
        }

        if (_isSelecting
            && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            SetCaretFromContentPoint(e.GetPosition(this), extend: true);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_imageResize is { } resize)
        {
            var preview = _imageResizePreview;
            _imageResize = null;
            _imageResizePreview = null;
            e.Pointer.Capture(null);

            if (preview is { } result)
            {
                EditorHost?.ResizeImage(
                    resize.ParagraphId,
                    resize.RunId,
                    result.WidthPoints,
                    result.HeightPoints,
                    resize.LockAspectRatio);
            }
        }
        else if (_isSelecting)
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

    private bool TryGetImageAt(
        Point contentPoint,
        out (Guid ParagraphId, Guid RunId) image)
    {
        if (TryGetImagePlacementAt(
            contentPoint,
            out var paragraphId,
            out var placement,
            out _,
            out _))
        {
            image = (paragraphId, placement.RunId);
            return true;
        }

        image = default;
        return false;
    }

    private bool TryGetImagePlacementAt(
        Point contentPoint,
        out Guid paragraphId,
        out InlineImageLayout placement,
        out PageLayout page,
        out Rect pageRect)
    {
        paragraphId = Guid.Empty;
        placement = null!;
        page = null!;
        pageRect = default;

        if (Layout is null
            || !TryGetPageAt(contentPoint, out var hitPage, out var hitRect, out _))
        {
            return false;
        }

        var scale = PageViewport.PageScale(Zoom);
        var pageX = ((contentPoint.X - hitRect.X) / scale) + hitPage.Bounds.X;
        var pageY = ((contentPoint.Y - hitRect.Y) / scale) + hitPage.Bounds.Y;

        foreach (var paragraph in hitPage.Paragraphs)
        {
            foreach (var line in paragraph.Lines)
            {
                foreach (var candidate in line.Images)
                {
                    var bounds = candidate.Bounds;

                    if (pageX >= bounds.X
                        && pageX <= bounds.Right
                        && pageY >= bounds.Y
                        && pageY <= bounds.Bottom)
                    {
                        paragraphId = paragraph.ParagraphId;
                        placement = candidate;
                        page = hitPage;
                        pageRect = hitRect;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    internal bool SelectImageAt(Point contentPoint)
    {
        if (!TryGetImageAt(contentPoint, out var image))
        {
            return false;
        }

        Selection = new Selection(
            new TextPosition(image.ParagraphId, image.RunId, 0),
            new TextPosition(image.ParagraphId, image.RunId, 1));
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
        var pageBreak = Layout.PageBreaks.FirstOrDefault(candidate =>
            candidate.PageNumber == page.PageNumber
            && Math.Abs(contentPoint.Y - (pageRect.Y + (candidate.Y * scale))) <= 6);

        if (pageBreak is not null)
        {
            var breakParagraph = Document.FindParagraph(pageBreak.ParagraphId);

            if (breakParagraph is not null)
            {
                position = TextNavigation.FromParagraphTextOffset(
                    breakParagraph,
                    pageBreak.TextOffset);

                return true;
            }
        }

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
            case Key.Tab:
                if (IsEditable
                    && EditorHost?.TryMoveTableCell(
                        !modifiers.HasFlag(KeyModifiers.Shift)) == true)
                {
                    return true;
                }

                return false;
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

    private static void DrawTables(
        DrawingContext context,
        PageLayout page,
        Rect pageRect,
        double scale)
    {
        foreach (var table in page.Tables)
        {
            foreach (var row in table.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    var rect = new Rect(
                        pageRect.X + (cell.Bounds.X * scale),
                        pageRect.Y + (cell.Bounds.Y * scale),
                        cell.Bounds.Width * scale,
                        cell.Bounds.Height * scale);
                    var shading = cell.ShadingHex
                        ?? (row.IsHeader ? "#F2F2F2" : null);

                    if (shading is not null)
                    {
                        context.FillRectangle(CreateBrush(shading), rect);
                    }

                    var borders = cell.Borders;

                    if (borders is not null
                        && (borders.Left || borders.Top || borders.Right || borders.Bottom))
                    {
                        var pen = new Pen(
                            CreateBrush(borders.ColorHex),
                            Math.Max(0.5, borders.WidthPoints * scale));
                        context.DrawRectangle(null, pen, rect);
                    }
                }
            }
        }
    }

    private static IEnumerable<ParagraphLayout> PageParagraphs(PageLayout page) =>
        page.Paragraphs
            .Concat(page.Tables
                .SelectMany(table => table.Rows)
                .SelectMany(row => row.Cells)
                .SelectMany(cell => cell.Paragraphs));

    private static void DrawListLabels(
        DrawingContext context,
        PageLayout page,
        Rect pageRect,
        double scale)
    {
        foreach (var paragraph in PageParagraphs(page))
        {
            if (string.IsNullOrEmpty(paragraph.ListLabel)
                || paragraph.Lines.Length == 0)
            {
                continue;
            }

            var line = paragraph.Lines[0];
            var span = line.Spans.FirstOrDefault();
            var font = span?.Font ?? line.ResolvedFont;
            var fontSize = span?.FontSizePoints ?? line.FontSizePoints;
            var formatted = CreateFormattedText(
                paragraph.ListLabel,
                font,
                fontSize * scale);

            context.DrawText(
                formatted,
                new Point(
                    pageRect.X + (paragraph.ListLabelX * scale),
                    pageRect.Y + (line.Bounds.Y * scale)));
        }
    }

    private void DrawPageBreaks(
        DrawingContext context,
        DocumentLayout layout,
        PageLayout page,
        Rect pageRect,
        double scale)
    {
        var selection = Document is null
            ? default
            : SelectionMap.Normalize(Selection, Document);
        var hasSelection = Document is not null && !selection.IsCollapsed;

        foreach (var pageBreak in layout.PageBreaks)
        {
            if (pageBreak.PageNumber != page.PageNumber)
            {
                continue;
            }

            var y = pageRect.Y + (pageBreak.Y * scale);

            if (hasSelection && Document is not null)
            {
                var paragraph = Document.FindParagraph(pageBreak.ParagraphId);

                if (paragraph is not null)
                {
                    var position = TextNavigation.FromParagraphTextOffset(
                        paragraph,
                        pageBreak.TextOffset);
                    var start = selection.GetStart(Document);
                    var end = selection.GetEnd(Document);

                    if (TextNavigation.Compare(Document, start, position) <= 0
                        && TextNavigation.Compare(Document, position, end) <= 0)
                    {
                        context.FillRectangle(
                            SelectionBrush,
                            new Rect(pageRect.X, y - 3, pageRect.Width, 6));
                    }
                }
            }

            context.DrawLine(
                PageBreakPen,
                new Point(pageRect.X, y),
                new Point(pageRect.Right, y));

            var formatted = new FormattedText(
                "Page break",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                10,
                PageBreakBrush);

            context.DrawText(formatted, new Point(pageRect.X + 4, y - 14));
        }
    }

    private void DrawLine(
        DrawingContext context,
        LineLayout line,
        Rect pageRect,
        double scale)
    {
        if (line.Text.Length == 0)
        {
            return;
        }

        var visibleText = line.Text.Replace(
            InlineImage.PlaceholderCharacter,
            ' ');

        if (line.Spans.Length == 0 && !string.IsNullOrWhiteSpace(visibleText))
        {
            DrawSpan(
                context,
                visibleText,
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
        }
        else
        {
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
                    line.Text.Substring(span.TextStart, span.TextLength)
                        .Replace(InlineImage.PlaceholderCharacter, ' '),
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

        foreach (var image in line.Images)
        {
            DrawImage(context, image, pageRect, scale);
        }
    }

    private void DrawImage(
        DrawingContext context,
        InlineImageLayout placement,
        Rect pageRect,
        double scale)
    {
        var rect = new Rect(
            pageRect.X + (placement.Bounds.X * scale),
            pageRect.Y + (placement.Bounds.Y * scale),
            Math.Max(1, placement.Bounds.Width * scale),
            Math.Max(1, placement.Bounds.Height * scale));

        if (!placement.Image.IsPlaceholder)
        {
            var bitmap = GetImageBitmap(placement.Image);

            if (bitmap is not null)
            {
                context.DrawImage(bitmap, rect);
                return;
            }
        }

        context.FillRectangle(PlaceholderFill, rect);
        context.DrawRectangle(null, PlaceholderBorder, rect);

        var label = placement.Image.IsPlaceholder
            ? "Image unavailable"
            : placement.Image.AccessibleName;

        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        var formatted = new FormattedText(
            label,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            Math.Max(9, 11 * scale),
            TextBrush)
        {
            MaxTextWidth = Math.Max(1, rect.Width - 8),
            MaxTextHeight = Math.Max(1, rect.Height - 8),
            Trimming = TextTrimming.CharacterEllipsis,
        };

        context.DrawText(formatted, new Point(rect.X + 4, rect.Y + 4));
    }

    private Bitmap? GetImageBitmap(InlineImage image)
    {
        if (_imageCache.TryGetValue(image, out var cached))
        {
            return cached;
        }

        Bitmap? bitmap = null;

        try
        {
            using var stream = new MemoryStream(image.Data.ToArray(), writable: false);
            bitmap = new Bitmap(stream);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or IOException)
        {
            bitmap = null;
        }

        _imageCache[image] = bitmap;
        return bitmap;
    }

    private void DrawImageSelection(
        DrawingContext context,
        PageLayout page,
        Rect pageRect,
        double scale)
    {
        if (Document is null)
        {
            return;
        }

        var selected = ImageSelection.FindSelectedImage(Document, Selection);

        if (selected is null)
        {
            return;
        }

        foreach (var paragraph in page.Paragraphs)
        {
            foreach (var line in paragraph.Lines)
            {
                foreach (var placement in line.Images)
                {
                    if (placement.RunId != selected.Value.RunId)
                    {
                        continue;
                    }

                    var rect = new Rect(
                        pageRect.X + (placement.Bounds.X * scale),
                        pageRect.Y + (placement.Bounds.Y * scale),
                        Math.Max(1, placement.Bounds.Width * scale),
                        Math.Max(1, placement.Bounds.Height * scale));
                    context.DrawRectangle(null, ImageSelectionPen, rect);

                    const double handleSize = 7;
                    var half = handleSize / 2;

                    foreach (var point in new[]
                    {
                        rect.TopLeft,
                        rect.TopRight,
                        rect.BottomLeft,
                        rect.BottomRight,
                    })
                    {
                        var handle = new Rect(
                            point.X - half,
                            point.Y - half,
                            handleSize,
                            handleSize);
                        context.FillRectangle(ImageHandleFill, handle);
                        context.DrawRectangle(null, ImageSelectionPen, handle);
                    }

                    return;
                }
            }
        }
    }

    private bool TryStartImageResize(
        Point point,
        Guid paragraphId,
        InlineImageLayout placement,
        PageLayout page,
        Rect pageRect)
    {
        if (!IsEditable || Document is null)
        {
            return false;
        }

        var selected = ImageSelection.FindSelectedImage(Document, Selection);

        if (selected is null || selected.Value.RunId != placement.RunId)
        {
            return false;
        }

        var scale = PageViewport.PageScale(Zoom);
        var rect = ToScreenRect(placement.Bounds, pageRect, scale);

        foreach (var (corner, position) in new[]
        {
            (ResizeCorner.TopLeft, rect.TopLeft),
            (ResizeCorner.TopRight, rect.TopRight),
            (ResizeCorner.BottomLeft, rect.BottomLeft),
            (ResizeCorner.BottomRight, rect.BottomRight),
        })
        {
            if (Math.Abs(point.X - position.X) <= 8
                && Math.Abs(point.Y - position.Y) <= 8)
            {
                _imageResize = new ImageResizeState(
                    paragraphId,
                    placement.RunId,
                    placement,
                    page,
                    pageRect,
                    point,
                    corner,
                    placement.Image.LockAspectRatio,
                    placement.Image.AspectRatio);
                _imageResizePreview = new ImageResizePreview(
                    _imageResize.Value,
                    rect,
                    placement.Image.WidthPoints,
                    placement.Image.HeightPoints);
                InvalidateVisual();
                return true;
            }
        }

        return false;
    }

    private void UpdateImageResize(ImageResizeState state, Point point)
    {
        var scale = PageViewport.PageScale(Zoom);
        var dx = (point.X - state.StartPoint.X) / scale;
        var dy = (point.Y - state.StartPoint.Y) / scale;
        var original = state.Placement.Bounds;
        var width = original.Width;
        var height = original.Height;

        switch (state.Corner)
        {
            case ResizeCorner.TopLeft:
                width = original.Width - dx;
                height = original.Height - dy;
                break;
            case ResizeCorner.TopRight:
                width = original.Width + dx;
                height = original.Height - dy;
                break;
            case ResizeCorner.BottomLeft:
                width = original.Width - dx;
                height = original.Height + dy;
                break;
            default:
                width = original.Width + dx;
                height = original.Height + dy;
                break;
        }

        var page = Document?.Sections.FirstOrDefault()?.EffectivePage
            ?? PageSettings.Letter;
        var maxWidth = page.ContentWidthPoints;
        var maxHeight = page.ContentHeightPoints;
        width = Math.Clamp(width, 1, maxWidth);

        if (state.LockAspectRatio && state.AspectRatio > 0)
        {
            height = width / state.AspectRatio;

            if (height > maxHeight)
            {
                height = maxHeight;
                width = height * state.AspectRatio;
            }
        }
        else
        {
            height = Math.Clamp(height, 1, maxHeight);
        }

        var x = state.Corner is ResizeCorner.TopLeft or ResizeCorner.BottomLeft
            ? original.Right - width
            : original.X;
        var y = state.Corner is ResizeCorner.TopLeft or ResizeCorner.TopRight
            ? original.Bottom - height
            : original.Y;
        var modelRect = new LayoutRect(x, y, width, height);
        var screenRect = ToScreenRect(modelRect, state.PageRect, scale);

        _imageResizePreview = new ImageResizePreview(
            state,
            screenRect,
            width,
            height);
        InvalidateVisual();
    }

    private void DrawImageResizePreview(DrawingContext context)
    {
        if (_imageResizePreview is not { } preview)
        {
            return;
        }

        context.DrawRectangle(
            new SolidColorBrush(Color.Parse("#EAF3FF")) { Opacity = 0.25 },
            ImageSelectionPen,
            preview.Rect);
    }

    private static Rect ToScreenRect(
        LayoutRect bounds,
        Rect pageRect,
        double scale) =>
        new(
            pageRect.X + (bounds.X * scale),
            pageRect.Y + (bounds.Y * scale),
            Math.Max(1, bounds.Width * scale),
            Math.Max(1, bounds.Height * scale));

    private enum ResizeCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
    }

    private readonly record struct ImageResizeState(
        Guid ParagraphId,
        Guid RunId,
        InlineImageLayout Placement,
        PageLayout Page,
        Rect PageRect,
        Point StartPoint,
        ResizeCorner Corner,
        bool LockAspectRatio,
        double AspectRatio);

    private readonly record struct ImageResizePreview(
        ImageResizeState State,
        Rect Rect,
        double WidthPoints,
        double HeightPoints);

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

    private void DrawSearchHighlights(
        DrawingContext context,
        PageLayout page,
        Rect pageRect,
        double scale)
    {
        var document = Document;

        if (document is null
            || Layout is null
            || SearchHighlights is not { Count: > 0 } highlights)
        {
            return;
        }

        var paragraphs = GetDocumentParagraphs(document);

        foreach (var highlight in highlights)
        {
            var selection = SelectionMap.Normalize(highlight, document);

            if (!selection.IsCollapsed)
            {
                DrawRange(
                    context,
                    page,
                    pageRect,
                    scale,
                    paragraphs,
                    selection,
                    SearchHighlightBrush);
            }
        }
    }

    private void DrawSelection(
        DrawingContext context,
        PageLayout page,
        Rect pageRect,
        double scale)
    {
        var document = Document;

        if (document is null || Layout is null)
        {
            return;
        }

        var selection = SelectionMap.Normalize(Selection, document);

        if (selection.IsCollapsed)
        {
            return;
        }

        DrawRange(
            context,
            page,
            pageRect,
            scale,
            GetDocumentParagraphs(document),
            selection,
            SelectionBrush);
    }

    private void DrawRange(
        DrawingContext context,
        PageLayout page,
        Rect pageRect,
        double scale,
        List<ParagraphBlock> paragraphs,
        Selection selection,
        IBrush brush)
    {
        var document = Document;

        if (document is null)
        {
            return;
        }

        var start = selection.GetStart(document);
        var end = selection.GetEnd(document);
        var startIndex = FindParagraphIndex(paragraphs, start.ParagraphId);
        var endIndex = FindParagraphIndex(paragraphs, end.ParagraphId);

        if (startIndex < 0 || endIndex < 0)
        {
            return;
        }

        var startOffset = GetParagraphOffset(start);
        var endOffset = GetParagraphOffset(end);

        foreach (var paragraphLayout in PageParagraphs(page))
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

                context.FillRectangle(brush, rect);
            }
        }
    }

    private static List<ParagraphBlock> GetDocumentParagraphs(Document document) =>
        document.EnumerateParagraphs().ToList();

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
        PageParagraphs(page).Sum(paragraph => paragraph.Lines.Length);

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
