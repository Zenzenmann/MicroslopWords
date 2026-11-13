using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicroslopWords.App.Controls;
using MicroslopWords.App.Services;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using MicroslopWords.Core.Styles;
using MicroslopWords.Formats.Docx;
using MicroslopWords.Layout;

namespace MicroslopWords.App.ViewModels;

public partial class MainViewModel : ViewModelBase, ITextEditorHost, IDisposable
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    public static readonly TimeSpan MinimumAutosaveInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MaximumAutosaveInterval = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan DefaultAutosaveInterval = TimeSpan.FromSeconds(60);

    private readonly IDocxFilePicker _filePicker;
    private readonly IDocxDocumentLoader _documentLoader;
    private readonly IDocxDocumentSaver _documentSaver;
    private readonly ISaveConfirmation _saveConfirmation;
    private readonly IRecoverySnapshotStore? _recoveryStore;
    private readonly IRecoveryPrompt? _recoveryPrompt;
    private readonly TimeSpan _autosaveInterval;
    private readonly TimeProvider _timeProvider;
    private readonly IClipboardService _clipboardService;
    private readonly IImageFilePicker _imageFilePicker;
    private readonly IImageInsertDialog _imageInsertDialog;
    private readonly IImageDecoder _imageDecoder;
    private readonly LayoutEngine _layoutEngine;
    private CommandHistory _history;
    private Guid? _selectedImageParagraphId;
    private Guid? _selectedImageRunId;
    private IReadOnlyList<Diagnostic> _documentDiagnostics = [];
    private CancellationTokenSource? _saveCancellation;
    private CancellationTokenSource? _autosaveCancellation;
    private bool _recoveryWarningShown;
    private bool _recoveryUnavailableNoticeShown;
    private bool _applyingFit;
    private bool _updatingSelection;
    private bool _updatingStyles;
    private bool _styleEditorIsNew;
    private string? _editingStyleId;
    private string? _stylePendingDeleteId;
    private RunProperties? _pendingCharacterProperties;
    private int _documentGeneration;

    public MainViewModel()
        : this(
            new UnavailableDocxFilePicker(),
            new DocxDocumentLoader(),
            new LayoutEngine(),
            new DocxDocumentSaver(),
            new UnavailableSaveConfirmation())
    {
    }

    public MainViewModel(
        IDocxFilePicker filePicker,
        IDocxDocumentLoader documentLoader,
        LayoutEngine layoutEngine)
        : this(
            filePicker,
            documentLoader,
            layoutEngine,
            new DocxDocumentSaver(),
            new UnavailableSaveConfirmation())
    {
    }

    public MainViewModel(
        IDocxFilePicker filePicker,
        IDocxDocumentLoader documentLoader,
        LayoutEngine layoutEngine,
        IDocxDocumentSaver documentSaver,
        ISaveConfirmation saveConfirmation)
        : this(
            filePicker,
            documentLoader,
            layoutEngine,
            documentSaver,
            saveConfirmation,
            recoveryStore: null,
            recoveryPrompt: null,
            DefaultAutosaveInterval,
            TimeProvider.System)
    {
    }

    public MainViewModel(
        IDocxFilePicker filePicker,
        IDocxDocumentLoader documentLoader,
        LayoutEngine layoutEngine,
        IDocxDocumentSaver documentSaver,
        ISaveConfirmation saveConfirmation,
        IRecoverySnapshotStore recoveryStore,
        IRecoveryPrompt recoveryPrompt,
        IClipboardService? clipboardService = null)
        : this(
            filePicker,
            documentLoader,
            layoutEngine,
            documentSaver,
            saveConfirmation,
            recoveryStore,
            recoveryPrompt,
            DefaultAutosaveInterval,
            TimeProvider.System,
            clipboardService)
    {
    }

    public MainViewModel(
        IDocxFilePicker filePicker,
        IDocxDocumentLoader documentLoader,
        LayoutEngine layoutEngine,
        IDocxDocumentSaver documentSaver,
        ISaveConfirmation saveConfirmation,
        IRecoverySnapshotStore recoveryStore,
        IRecoveryPrompt recoveryPrompt,
        TimeSpan autosaveInterval)
        : this(
            filePicker,
            documentLoader,
            layoutEngine,
            documentSaver,
            saveConfirmation,
            recoveryStore,
            recoveryPrompt,
            ValidateAutosaveInterval(autosaveInterval),
            TimeProvider.System)
    {
    }

    public MainViewModel(
        IDocxFilePicker filePicker,
        IDocxDocumentLoader documentLoader,
        LayoutEngine layoutEngine,
        IDocxDocumentSaver documentSaver,
        ISaveConfirmation saveConfirmation,
        IRecoverySnapshotStore recoveryStore,
        IRecoveryPrompt recoveryPrompt,
        IClipboardService? clipboardService,
        IImageFilePicker imageFilePicker,
        IImageInsertDialog imageInsertDialog,
        IImageDecoder imageDecoder)
        : this(
            filePicker,
            documentLoader,
            layoutEngine,
            documentSaver,
            saveConfirmation,
            recoveryStore,
            recoveryPrompt,
            DefaultAutosaveInterval,
            TimeProvider.System,
            clipboardService,
            imageFilePicker,
            imageInsertDialog,
            imageDecoder)
    {
    }

    internal MainViewModel(
        IDocxFilePicker filePicker,
        IDocxDocumentLoader documentLoader,
        LayoutEngine layoutEngine,
        IDocxDocumentSaver documentSaver,
        ISaveConfirmation saveConfirmation,
        IRecoverySnapshotStore? recoveryStore,
        IRecoveryPrompt? recoveryPrompt,
        TimeSpan autosaveInterval,
        TimeProvider timeProvider,
        IClipboardService? clipboardService = null,
        IImageFilePicker? imageFilePicker = null,
        IImageInsertDialog? imageInsertDialog = null,
        IImageDecoder? imageDecoder = null)
    {
        ArgumentNullException.ThrowIfNull(filePicker);
        ArgumentNullException.ThrowIfNull(documentLoader);
        ArgumentNullException.ThrowIfNull(layoutEngine);
        ArgumentNullException.ThrowIfNull(documentSaver);
        ArgumentNullException.ThrowIfNull(saveConfirmation);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (recoveryStore is not null && recoveryPrompt is null)
        {
            throw new ArgumentNullException(nameof(recoveryPrompt));
        }

        if (autosaveInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(autosaveInterval),
                "The autosave interval must be positive.");
        }

        _filePicker = filePicker;
        _documentLoader = documentLoader;
        _layoutEngine = layoutEngine;
        _documentSaver = documentSaver;
        _saveConfirmation = saveConfirmation;
        _recoveryStore = recoveryStore;
        _recoveryPrompt = recoveryPrompt;
        _autosaveInterval = autosaveInterval;
        _timeProvider = timeProvider;
        _clipboardService = clipboardService ?? UnavailableClipboardService.Instance;
        _imageFilePicker = imageFilePicker ?? new UnavailableImageFilePicker();
        _imageInsertDialog = imageInsertDialog ?? new UnavailableImageInsertDialog();
        _imageDecoder = imageDecoder ?? new HeaderOnlyImageDecoder();
        _history = new CommandHistory(
            Document.CreateEmpty(),
            timeProvider: _timeProvider);

        ApplyDocument(_history.Current, filePath: null);
        StatusText = "Ready.";
    }

    private static TimeSpan ValidateAutosaveInterval(TimeSpan autosaveInterval)
    {
        if (autosaveInterval < MinimumAutosaveInterval
            || autosaveInterval > MaximumAutosaveInterval)
        {
            throw new ArgumentOutOfRangeException(
                nameof(autosaveInterval),
                $"The autosave interval must be between "
                + $"{MinimumAutosaveInterval.TotalSeconds:0} and "
                + $"{MaximumAutosaveInterval.TotalSeconds:0} seconds.");
        }

        return autosaveInterval;
    }

    [ObservableProperty]
    public partial string DocumentTitle { get; set; } = "Untitled";

    [ObservableProperty]
    public partial string WindowTitle { get; set; } = "Untitled - MicroslopWords";

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Ready";

    [ObservableProperty]
    public partial string FilePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsModified { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    [ObservableProperty]
    public partial bool IsProgressVisible { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DiagnosticsSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DocumentLayout? Layout { get; set; }

    [ObservableProperty]
    public partial Document Document { get; set; } =
        MicroslopWords.Core.Documents.Document.CreateEmpty();

    [ObservableProperty]
    public partial Selection Selection { get; set; }

    [ObservableProperty]
    public partial bool IsReadOnly { get; set; } = true;

    [ObservableProperty]
    public partial bool HasSelectedImage { get; set; }

    [ObservableProperty]
    public partial string SelectedImageDescription { get; set; } = string.Empty;

    public bool CanEditImage => HasSelectedImage && IsEditingEnabled;

    public bool CanRemoveImage => HasSelectedImage && IsEditingEnabled;

    [ObservableProperty]
    public partial double Zoom { get; set; } = 1.0;

    [ObservableProperty]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    public partial ZoomFitMode ZoomMode { get; set; } = ZoomFitMode.Custom;

    [ObservableProperty]
    public partial bool? IsBoldSelected { get; set; }

    [ObservableProperty]
    public partial bool? IsItalicSelected { get; set; }

    [ObservableProperty]
    public partial bool? IsUnderlineSelected { get; set; }

    [ObservableProperty]
    public partial bool? IsStrikethroughSelected { get; set; }

    [ObservableProperty]
    public partial string? SelectedFontFamily { get; set; }

    [ObservableProperty]
    public partial double? SelectedFontSize { get; set; }

    [ObservableProperty]
    public partial string? SelectedColorHex { get; set; }

    [ObservableProperty]
    public partial string? SelectedHighlightHex { get; set; }

    [ObservableProperty]
    public partial bool IsHighlightMixed { get; set; }

    [ObservableProperty]
    public partial string FontFamilyInput { get; set; } = "Arial";

    [ObservableProperty]
    public partial string FontSizeInput { get; set; } = "11";

    [ObservableProperty]
    public partial string ColorInput { get; set; } = "#000000";

    [ObservableProperty]
    public partial string HighlightInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ParagraphAlignment? SelectedAlignment { get; set; }

    [ObservableProperty]
    public partial double? SelectedSpaceBefore { get; set; }

    [ObservableProperty]
    public partial double? SelectedSpaceAfter { get; set; }

    [ObservableProperty]
    public partial double? SelectedLineSpacing { get; set; }

    [ObservableProperty]
    public partial double? SelectedLeftIndent { get; set; }

    [ObservableProperty]
    public partial double? SelectedRightIndent { get; set; }

    [ObservableProperty]
    public partial double? SelectedFirstLineIndent { get; set; }

    [ObservableProperty]
    public partial double? SelectedHangingIndent { get; set; }

    [ObservableProperty]
    public partial bool? IsBulletedList { get; set; }

    [ObservableProperty]
    public partial bool? IsNumberedList { get; set; }

    [ObservableProperty]
    public partial int? SelectedListLevel { get; set; }

    [ObservableProperty]
    public partial string SpaceBeforeInput { get; set; } = "0";

    [ObservableProperty]
    public partial string SpaceAfterInput { get; set; } = "8";

    [ObservableProperty]
    public partial string LineSpacingInput { get; set; } = "1.15";

    [ObservableProperty]
    public partial string LeftIndentInput { get; set; } = "0";

    [ObservableProperty]
    public partial string RightIndentInput { get; set; } = "0";

    [ObservableProperty]
    public partial string FirstLineIndentInput { get; set; } = "0";

    [ObservableProperty]
    public partial string HangingIndentInput { get; set; } = "0";

    [ObservableProperty]
    public partial bool IsStylesPanelOpen { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<StyleListItem> StyleItems { get; set; } = [];

    [ObservableProperty]
    public partial StyleListItem? SelectedStyle { get; set; }

    [ObservableProperty]
    public partial string CurrentParagraphStyleName { get; set; } = "Normal";

    [ObservableProperty]
    public partial string CurrentCharacterStyleName { get; set; } = "Default";

    [ObservableProperty]
    public partial bool IsStyleMixed { get; set; }

    [ObservableProperty]
    public partial bool IsStyleEditorOpen { get; set; }

    [ObservableProperty]
    public partial bool IsStyleDeleteConfirmationOpen { get; set; }

    [ObservableProperty]
    public partial string StyleEditorName { get; set; } = "New Style";

    [ObservableProperty]
    public partial string StyleEditorKind { get; set; } = "Paragraph";

    [ObservableProperty]
    public partial bool IsStyleKindEditable { get; set; } = true;

    public IReadOnlyList<string> StyleKinds { get; } = ["Paragraph", "Character"];

    [ObservableProperty]
    public partial string StyleDeletePrompt { get; set; } = string.Empty;

    public bool IsFontFamilyMixed => SelectedFontFamily is null;

    public bool IsFontSizeMixed => SelectedFontSize is null;

    public bool IsColorMixed => SelectedColorHex is null;

    public bool IsAlignmentMixed => SelectedAlignment is null;

    public bool AreParagraphValuesMixed =>
        SelectedAlignment is null
        || SelectedSpaceBefore is null
        || SelectedSpaceAfter is null
        || SelectedLineSpacing is null
        || SelectedLeftIndent is null
        || SelectedRightIndent is null
        || SelectedFirstLineIndent is null
        || SelectedHangingIndent is null;

    public double ViewportWidth { get; private set; }

    public double ViewportHeight { get; private set; }

    public bool IsEditingEnabled => !IsReadOnly && !IsLoading && !IsSaving;

    public bool CanStartAction => !IsLoading && !IsSaving;

    public bool CanCancelSave => IsSaving;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasDiagnostics => !string.IsNullOrEmpty(DiagnosticsSummary);

    public string SaveStateText =>
        IsSaving
            ? "Saving..."
            : IsModified
                ? "Unsaved changes"
                : string.IsNullOrEmpty(FilePath)
                    ? "Not saved"
                    : "Saved";

    public int PageCount => Layout?.Pages.Length ?? 0;

    public string PageLabel =>
        PageCount == 0
            ? "Page 1 of 1"
            : $"Page {Math.Clamp(CurrentPage, 1, PageCount)} of {PageCount}";

    public string ZoomPercentText => $"{Zoom * 100:0}%";

    public CommandHistory History => _history;

    [RelayCommand(CanExecute = nameof(CanStartAction))]
    private void NewDocument()
    {
        _documentGeneration++;

        var document = Document.CreateEmpty();
        ApplyDocument(document, filePath: null);
        ClearMessages();
        StatusText = "New document created.";
    }

    [RelayCommand(CanExecute = nameof(CanStartAction), IncludeCancelCommand = true)]
    private async Task OpenDocumentAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? progressCancellation = null;

        try
        {
            var path = await _filePicker.PickOpenFileAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(path))
            {
                StatusText = "Open canceled.";
                return;
            }

            var generation = ++_documentGeneration;
            ClearMessages();
            IsLoading = true;

            progressCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var progressTask = ShowProgressAfterDelayAsync(progressCancellation.Token);

            var result = await Task.Run(
                () => _documentLoader.Load(path, cancellationToken),
                cancellationToken);

            progressCancellation.Cancel();
            await progressTask;

            if (generation != _documentGeneration || cancellationToken.IsCancellationRequested)
            {
                StatusText = "Open canceled.";
                return;
            }

            if (!result.IsSuccess || result.Value is null)
            {
                ShowOpenFailure(path, result.Diagnostics);
                return;
            }

            var warnings = result.Diagnostics
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning)
                .ToArray();

            ApplyDocument(result.Value, path, warnings);

            if (warnings.Length > 0)
            {
                DiagnosticsSummary = string.Join(" ", warnings.Select(diagnostic => diagnostic.Message));
                StatusText = $"Opened {DocumentTitle} with {warnings.Length} unsupported item(s).";
            }
            else
            {
                StatusText = $"Opened {DocumentTitle}.";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Open canceled.";
        }
        catch (UnauthorizedAccessException)
        {
            ShowOpenFailure(
                string.Empty,
                [new Diagnostic(
                    DocxDiagnosticCodes.FileUnreadable,
                    DiagnosticSeverity.Error,
                    "The selected file cannot be read.")]);
        }
        catch (IOException)
        {
            ShowOpenFailure(
                string.Empty,
                [new Diagnostic(
                    DocxDiagnosticCodes.FileUnreadable,
                    DiagnosticSeverity.Error,
                    "The selected file is locked or cannot be read.")]);
        }
        finally
        {
            progressCancellation?.Cancel();
            progressCancellation?.Dispose();
            IsProgressVisible = false;
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartAction))]
    private Task SaveDocumentAsync() => SaveAsync(saveAs: false);

    [RelayCommand(CanExecute = nameof(CanStartAction))]
    private Task SaveDocumentAsAsync() => SaveAsync(saveAs: true);

    [RelayCommand(CanExecute = nameof(CanCancelSave))]
    private void CancelSave() => _saveCancellation?.Cancel();

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ToggleBold() => ToggleCharacterProperty(CharacterProperty.Bold);

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ToggleItalic() => ToggleCharacterProperty(CharacterProperty.Italic);

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ToggleUnderline() => ToggleCharacterProperty(CharacterProperty.Underline);

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ToggleStrikethrough() =>
        ToggleCharacterProperty(CharacterProperty.Strikethrough);

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ClearCharacterFormatting()
    {
        if (_history.Selection.IsCollapsed)
        {
            var effective = EffectivePropertiesAtCaret();
            _pendingCharacterProperties = new RunProperties
            {
                StyleId = effective.StyleId,
            };
            RefreshCharacterFormattingState();
            return;
        }

        ExecuteEdit(new ClearCharacterFormattingCommand(_history.Selection));
        _pendingCharacterProperties = null;
        RefreshCharacterFormattingState();
    }

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ApplyFontFamily()
    {
        var family = FontFamilyInput.Trim();

        if (family.Length == 0)
        {
            ErrorMessage = "Font family cannot be empty.";
            return;
        }

        SetFontFamily(family);
    }

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ApplyFontSize()
    {
        if (!double.TryParse(
            FontSizeInput,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var size))
        {
            ErrorMessage = "Font size must be a number between 1 and 400 pt.";
            return;
        }

        if (size is < 1 or > 400)
        {
            ErrorMessage = "Font size must be between 1 and 400 pt.";
            return;
        }

        SetFontSize(size);
    }

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ApplyColor()
    {
        if (!TryNormalizeColor(ColorInput, out var color))
        {
            ErrorMessage = "Text color must use the #RRGGBB format.";
            return;
        }

        SetColor(color);
    }

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ApplyHighlight()
    {
        if (string.IsNullOrWhiteSpace(HighlightInput))
        {
            SetHighlight(null);
            return;
        }

        if (!TryNormalizeColor(HighlightInput, out var highlight))
        {
            ErrorMessage = "Highlight color must use the #RRGGBB format.";
            return;
        }

        SetHighlight(highlight);
    }

    [RelayCommand]
    private void Find() => OpenFindPanel();

    public void SetFontFamily(string family) =>
        ApplyCharacterProperties(run => run with { FontFamily = family });

    public void SetFontSize(double points) =>
        ApplyCharacterProperties(run => run with { FontSizePoints = points });

    public void SetColor(string colorHex) =>
        ApplyCharacterProperties(run => run with { ColorHex = colorHex });

    public void SetHighlight(string? highlightHex) =>
        ApplyCharacterProperties(run => run with { HighlightHex = highlightHex });

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void AlignLeft() =>
        ApplyParagraphProperties(properties => properties with
        {
            Alignment = ParagraphAlignment.Left,
        });

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void AlignCenter() =>
        ApplyParagraphProperties(properties => properties with
        {
            Alignment = ParagraphAlignment.Center,
        });

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void AlignRight() =>
        ApplyParagraphProperties(properties => properties with
        {
            Alignment = ParagraphAlignment.Right,
        });

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void AlignJustify() =>
        ApplyParagraphProperties(properties => properties with
        {
            Alignment = ParagraphAlignment.Justify,
        });

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ToggleBulletedList()
    {
        ExecuteEdit(new ToggleListCommand(_history.Selection, ListKind.Bulleted));
        RefreshParagraphFormattingState();
    }

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ToggleNumberedList()
    {
        ExecuteEdit(new ToggleListCommand(_history.Selection, ListKind.Numbered));
        RefreshParagraphFormattingState();
    }

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void IncreaseListLevel() =>
        SetListLevel(Math.Min(9, (SelectedListLevel ?? 1) + 1));

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void DecreaseListLevel() =>
        SetListLevel(Math.Max(1, (SelectedListLevel ?? 1) - 1));

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void RestartNumbering()
    {
        ExecuteEdit(new RestartNumberingCommand(_history.Selection));
        RefreshParagraphFormattingState();
    }

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void InsertPageBreak()
    {
        ExecuteEdit(new InsertPageBreakCommand(_history.Selection));
        RefreshParagraphFormattingState();
    }

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ClearParagraphFormatting()
    {
        ExecuteEdit(new ClearParagraphFormattingCommand(_history.Selection));
        RefreshParagraphFormattingState();
    }

    [RelayCommand]
    private void ToggleStylesPanel() => IsStylesPanelOpen = !IsStylesPanelOpen;

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void NewStyle()
    {
        _styleEditorIsNew = true;
        _editingStyleId = null;
        StyleEditorName = "New Style";
        StyleEditorKind = "Paragraph";
        IsStyleKindEditable = true;
        IsStyleDeleteConfirmationOpen = false;
        IsStyleEditorOpen = true;
    }

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ModifyStyle()
    {
        if (SelectedStyle is null)
        {
            return;
        }

        _styleEditorIsNew = false;
        _editingStyleId = SelectedStyle.Id;
        StyleEditorName = SelectedStyle.Name;
        StyleEditorKind = SelectedStyle.Kind;
        IsStyleKindEditable = false;
        IsStyleDeleteConfirmationOpen = false;
        IsStyleEditorOpen = true;
    }

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void SaveStyle()
    {
        if (string.IsNullOrWhiteSpace(StyleEditorName))
        {
            ErrorMessage = "Style name is required.";
            return;
        }

        try
        {
            if (_styleEditorIsNew)
            {
                if (string.Equals(StyleEditorKind, "Character", StringComparison.Ordinal))
                {
                    var command = new CreateCharacterStyleCommand(
                        _history.Selection,
                        StyleEditorName);
                    ExecuteEdit(command);
                    _pendingCharacterProperties = _history.Selection.IsCollapsed
                        ? new RunProperties { StyleId = command.StyleId }
                        : null;
                }
                else
                {
                    ExecuteEdit(new CreateParagraphStyleCommand(
                        _history.Selection,
                        StyleEditorName));
                }
            }
            else if (_editingStyleId is not null)
            {
                ExecuteEdit(new RenameStyleCommand(
                    _history.Selection,
                    _editingStyleId,
                    StyleEditorName));
            }

            IsStyleEditorOpen = false;
            ErrorMessage = string.Empty;
            RefreshStyleState();
        }
        catch (InvalidOperationException exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void CancelStyleEditor() => IsStyleEditorOpen = false;

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void UpdateStyleFromSelection()
    {
        if (SelectedStyle is null)
        {
            return;
        }

        var sheet = Document.EffectiveStyles;
        var style = sheet.FindParagraphStyle(SelectedStyle.Id);

        try
        {
            if (style is not null)
            {
                var paragraph = GetSelectedParagraphs().FirstOrDefault();

                if (paragraph is null)
                {
                    return;
                }

                var paragraphProperties = StyleResolver.ResolveParagraph(paragraph, sheet);
                var characterProperties = paragraph.Runs.Length > 0
                    ? StyleResolver.ResolveRun(paragraph.Runs[0], paragraph, sheet)
                    : style.CharacterProperties ?? RunProperties.Default;
                ExecuteEdit(new EditParagraphStyleCommand(
                    _history.Selection,
                    style.Id,
                    paragraphProperties,
                    characterProperties));
            }
            else
            {
                var characterStyle = sheet.FindCharacterStyle(SelectedStyle.Id);

                if (characterStyle is null)
                {
                    return;
                }

                ExecuteEdit(new EditCharacterStyleCommand(
                    _history.Selection,
                    characterStyle.Id,
                    GetSelectedRunProperties().FirstOrDefault() ?? RunProperties.Default));
            }

            ErrorMessage = string.Empty;
            RefreshStyleState();
        }
        catch (InvalidOperationException exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void DeleteStyle()
    {
        if (SelectedStyle is null)
        {
            return;
        }

        if (SelectedStyle.Id is StyleSheet.NormalStyleId
            or StyleSheet.DefaultCharacterStyleId)
        {
            ErrorMessage = "The default style cannot be deleted.";
            return;
        }

        _stylePendingDeleteId = SelectedStyle.Id;
        StyleDeletePrompt = $"Delete style \"{SelectedStyle.Name}\"?";
        IsStyleDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    private void ConfirmDeleteStyle()
    {
        if (_stylePendingDeleteId is null)
        {
            IsStyleDeleteConfirmationOpen = false;
            return;
        }

        try
        {
            ExecuteEdit(new DeleteStyleCommand(_history.Selection, _stylePendingDeleteId));
            ErrorMessage = string.Empty;
            IsStyleDeleteConfirmationOpen = false;
            _stylePendingDeleteId = null;
            RefreshStyleState();
        }
        catch (InvalidOperationException exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void CancelDeleteStyle()
    {
        _stylePendingDeleteId = null;
        IsStyleDeleteConfirmationOpen = false;
    }

    public void ApplyStyle(StyleListItem style)
    {
        ArgumentNullException.ThrowIfNull(style);

        if (!IsEditingEnabled || IsStyleAlreadyApplied(style))
        {
            return;
        }

        try
        {
            if (string.Equals(style.Kind, "Character", StringComparison.Ordinal))
            {
                if (_history.Selection.IsCollapsed)
                {
                    _pendingCharacterProperties = new RunProperties
                    {
                        StyleId = style.Id,
                    };
                    RefreshCharacterFormattingState();
                    RefreshStyleState();
                    return;
                }

                ExecuteEdit(new ApplyCharacterStyleCommand(_history.Selection, style.Id));
            }
            else
            {
                ExecuteEdit(new ApplyParagraphStyleCommand(_history.Selection, style.Id));
            }

            ErrorMessage = string.Empty;
            RefreshStyleState();
        }
        catch (InvalidOperationException exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ApplySpaceBefore()
    {
        if (!TryParsePointValue(SpaceBeforeInput, out var value, allowNegative: false))
        {
            ErrorMessage = "Space before must be zero or greater.";
            return;
        }

        ApplyParagraphProperties(properties => properties with
        {
            SpaceBeforePoints = value,
        });
    }

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ApplySpaceAfter()
    {
        if (!TryParsePointValue(SpaceAfterInput, out var value, allowNegative: false))
        {
            ErrorMessage = "Space after must be zero or greater.";
            return;
        }

        ApplyParagraphProperties(properties => properties with
        {
            SpaceAfterPoints = value,
        });
    }

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ApplyLineSpacing()
    {
        if (!double.TryParse(
                LineSpacingInput,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value)
            || value is < 0.5 or > 3.0)
        {
            ErrorMessage = "Line spacing must be between 0.5 and 3.0.";
            return;
        }

        ApplyParagraphProperties(properties => properties with
        {
            LineSpacing = value,
        });
    }

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ApplyLeftIndent()
    {
        if (!TryParsePointValue(LeftIndentInput, out var value, allowNegative: false))
        {
            ErrorMessage = "Left indent must be zero or greater.";
            return;
        }

        ApplyParagraphProperties(properties => properties with
        {
            LeftIndentPoints = value,
        });
    }

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ApplyRightIndent()
    {
        if (!TryParsePointValue(RightIndentInput, out var value, allowNegative: false))
        {
            ErrorMessage = "Right indent must be zero or greater.";
            return;
        }

        ApplyParagraphProperties(properties => properties with
        {
            RightIndentPoints = value,
        });
    }

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ApplyFirstLineIndent()
    {
        if (!TryParsePointValue(FirstLineIndentInput, out var value, allowNegative: false))
        {
            ErrorMessage = "First-line indent must be zero or greater.";
            return;
        }

        ApplyParagraphProperties(properties => properties with
        {
            FirstLineIndentPoints = value,
        });
    }

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ApplyHangingIndent()
    {
        if (!TryParsePointValue(HangingIndentInput, out var value, allowNegative: false))
        {
            ErrorMessage = "Hanging indent must be zero or greater.";
            return;
        }

        ApplyParagraphProperties(properties => properties with
        {
            FirstLineIndentPoints = -value,
        });
    }

    private void SetListLevel(int level)
    {
        ExecuteEdit(new SetListLevelCommand(_history.Selection, level));
        RefreshParagraphFormattingState();
    }

    private void ApplyParagraphProperties(
        Func<ParagraphProperties, ParagraphProperties> transform)
    {
        if (!IsEditingEnabled)
        {
            return;
        }

        ErrorMessage = string.Empty;
        ExecuteEdit(new ApplyParagraphPropertiesCommand(_history.Selection, transform));
        RefreshParagraphFormattingState();
    }
    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (!_history.CanUndo)
        {
            return;
        }

        _history.Undo();
        FinishEdit();
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (!_history.CanRedo)
        {
            return;
        }

        _history.Redo();
        FinishEdit();
    }

    public bool HasSelection => !_history.Selection.IsCollapsed;

    public bool CanCopy => HasSelection;

    public bool CanCut => HasSelection && IsEditingEnabled;

    public bool CanPaste => IsEditingEnabled;

    public bool CanSelectAll => Document.Sections
        .SelectMany(section => section.Blocks)
        .OfType<ParagraphBlock>()
        .Any();

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private async Task CopyAsync()
    {
        if (!HasSelection)
        {
            return;
        }

        var content = CreateClipboardContent();
        var success = await _clipboardService.WriteAsync(content);

        if (success)
        {
            StatusText = "Copied.";
        }
        else
        {
            StatusText = "Clipboard is unavailable.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanCut))]
    private async Task CutAsync()
    {
        if (!HasSelection || !IsEditingEnabled)
        {
            return;
        }

        var selection = _history.Selection;
        var content = CreateClipboardContent();
        var success = await _clipboardService.WriteAsync(content);

        if (!success)
        {
            StatusText = "Clipboard is unavailable.";
            return;
        }

        ExecuteEdit(new DeleteSelectionCommand(selection));
        StatusText = "Cut.";
    }

    [RelayCommand(CanExecute = nameof(CanPaste))]
    private async Task PasteAsync()
    {
        if (!IsEditingEnabled)
        {
            return;
        }

        var content = await _clipboardService.ReadAsync();

        if (content is null)
        {
            StatusText = "Clipboard is unavailable.";
            return;
        }

        var fragment = content.Fragment;

        if ((fragment is null || fragment.IsEmpty)
            && !string.IsNullOrWhiteSpace(content.Html))
        {
            fragment = ClipboardHtml.Parse(content.Html);
        }

        if (fragment is { IsEmpty: false })
        {
            ExecuteEdit(new PasteFragmentCommand(_history.Selection, fragment));
            StatusText = "Pasted.";
            return;
        }

        if (!string.IsNullOrEmpty(content.Text))
        {
            ExecuteEdit(new PasteTextCommand(_history.Selection, content.Text));
            StatusText = "Pasted.";
            return;
        }

        if (content.ImagePng is { Length: > 0 } imageData)
        {
            var validation = _imageDecoder.Validate(imageData);

            if (!validation.IsSuccess || validation.Value is null)
            {
                ErrorMessage = validation.Diagnostics.FirstOrDefault()?.Message
                    ?? "The clipboard image is damaged.";
                return;
            }

            var page = CurrentPageSettings;
            var natural = ImageValidation.NaturalSize(
                validation.Value,
                page.ContentWidthPoints,
                page.ContentHeightPoints);

            if (!TryCreateImage(
                    imageData,
                    "Clipboard image",
                    natural.Width,
                    natural.Height,
                    lockAspectRatio: true,
                    altText: string.Empty,
                    page,
                    out var image,
                    out var error))
            {
                ErrorMessage = error;
                return;
            }

            ExecuteEdit(new InsertInlineImageCommand(_history.Selection, image!));
            ErrorMessage = string.Empty;
            StatusText = "Pasted image.";
            return;
        }

        StatusText = content.HasImage
            ? "Image paste failed: the clipboard image could not be read."
            : "Clipboard is empty.";
    }

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private async Task InsertImageAsync()
    {
        if (!IsEditingEnabled)
        {
            return;
        }

        var path = await _imageFilePicker.PickImageFileAsync();

        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText = "Insert image canceled.";
            return;
        }

        byte[] data;

        try
        {
            var info = new FileInfo(path);

            if (info.Length > ImageValidation.MaxImageBytes)
            {
                ErrorMessage = "Images must be 50 MB or smaller.";
                return;
            }

            data = await File.ReadAllBytesAsync(path);
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or NotSupportedException)
        {
            ErrorMessage = "The image file could not be read.";
            return;
        }

        var validation = _imageDecoder.Validate(data);

        if (!validation.IsSuccess || validation.Value is null)
        {
            ErrorMessage = validation.Diagnostics.FirstOrDefault()?.Message
                ?? "The image file is damaged or unsupported.";
            return;
        }

        var page = CurrentPageSettings;
        var natural = ImageValidation.NaturalSize(
            validation.Value,
            page.ContentWidthPoints,
            page.ContentHeightPoints);
        var request = new ImageDialogRequest(
            Path.GetFileName(path),
            data,
            validation.Value.PixelWidth,
            validation.Value.PixelHeight,
            natural.Width,
            natural.Height,
            LockAspectRatio: true,
            AltText: string.Empty,
            page.ContentWidthPoints,
            page.ContentHeightPoints);
        var options = await _imageInsertDialog.ShowAsync(request);

        if (options is null)
        {
            StatusText = "Insert image canceled.";
            return;
        }

        if (!TryCreateImage(
                data,
                Path.GetFileName(path),
                options.WidthPoints,
                options.HeightPoints,
                options.LockAspectRatio,
                options.AltText,
                page,
                out var image,
                out var error))
        {
            ErrorMessage = error;
            return;
        }

        ExecuteEdit(new InsertInlineImageCommand(_history.Selection, image!));
        ErrorMessage = string.Empty;
        StatusText = "Inserted image.";
    }

    [RelayCommand(CanExecute = nameof(CanEditImage))]
    private async Task EditImageAsync()
    {
        if (!TryGetSelectedImage(out var paragraphId, out var runId, out var image))
        {
            return;
        }

        var page = CurrentPageSettings;
        var request = new ImageDialogRequest(
            image.FileName,
            image.Data.ToArray(),
            image.PixelWidth,
            image.PixelHeight,
            image.WidthPoints,
            image.HeightPoints,
            image.LockAspectRatio,
            image.AltText,
            page.ContentWidthPoints,
            page.ContentHeightPoints);
        var options = await _imageInsertDialog.ShowAsync(request);

        if (options is null)
        {
            StatusText = "Image edit canceled.";
            return;
        }

        ExecuteEdit(new UpdateInlineImageCommand(
            _history.Selection,
            paragraphId,
            runId,
            options.WidthPoints,
            options.HeightPoints,
            options.LockAspectRatio,
            options.AltText));
        StatusText = "Image updated.";
    }

    [RelayCommand(CanExecute = nameof(CanRemoveImage))]
    private void RemoveImage()
    {
        if (TryGetSelectedImage(out var paragraphId, out var runId, out _))
        {
            ExecuteEdit(new DeleteInlineImageCommand(
                _history.Selection,
                paragraphId,
                runId));
            StatusText = "Image removed.";
        }
    }

    public void ResizeImage(
        Guid paragraphId,
        Guid runId,
        double widthPoints,
        double heightPoints,
        bool lockAspectRatio) =>
        ExecuteEdit(new ResizeInlineImageCommand(
            _history.Selection,
            paragraphId,
            runId,
            widthPoints,
            heightPoints,
            lockAspectRatio));

    private PageSettings CurrentPageSettings =>
        Document.Sections.FirstOrDefault()?.EffectivePage ?? PageSettings.Letter;

    private bool TryGetSelectedImage(
        out Guid paragraphId,
        out Guid runId,
        out InlineImage image)
    {
        paragraphId = Guid.Empty;
        runId = Guid.Empty;
        image = null!;

        if (_selectedImageParagraphId is not { } selectedParagraph
            || _selectedImageRunId is not { } selectedRun)
        {
            return false;
        }

        var paragraph = Document.FindParagraph(selectedParagraph);
        var runIndex = paragraph is null
            ? -1
            : TextNavigation.FindRunIndex(paragraph, selectedRun);

        if (paragraph is null
            || runIndex < 0
            || paragraph.Runs[runIndex].EffectiveImage is not { } selectedImage)
        {
            return false;
        }

        paragraphId = selectedParagraph;
        runId = selectedRun;
        image = selectedImage;
        return true;
    }

    private static bool TryCreateImage(
        byte[] data,
        string fileName,
        double widthPoints,
        double heightPoints,
        bool lockAspectRatio,
        string altText,
        PageSettings page,
        out InlineImage? image,
        out string error)
    {
        var result = ImageValidation.Create(
            data,
            fileName,
            altText,
            widthPoints,
            heightPoints,
            lockAspectRatio,
            page.ContentWidthPoints,
            page.ContentHeightPoints);

        image = result.Value;
        error = result.IsSuccess
            ? string.Empty
            : result.Diagnostics.FirstOrDefault()?.Message
                ?? "The image could not be inserted.";

        return result.IsSuccess && image is not null;
    }

    [RelayCommand(CanExecute = nameof(CanSelectAll))]
    private void SelectAll()
    {
        var start = Selection.StartOf(Document);
        var end = Selection.EndOf(Document);

        Selection = new Selection(start.Focus, end.Focus);
        StatusText = "Selected all.";
    }

    private ClipboardContent CreateClipboardContent()
    {
        var fragment = DocumentFragment.Capture(Document, _history.Selection);

        return new ClipboardContent(
            fragment.PlainText,
            ClipboardHtml.ToHtml(fragment),
            fragment);
    }

    private void NotifyClipboardCommands()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanCopy));
        OnPropertyChanged(nameof(CanCut));
        OnPropertyChanged(nameof(CanPaste));
        OnPropertyChanged(nameof(CanSelectAll));
        CopyCommand.NotifyCanExecuteChanged();
        CutCommand.NotifyCanExecuteChanged();
        PasteCommand.NotifyCanExecuteChanged();
        SelectAllCommand.NotifyCanExecuteChanged();
    }
    public void InsertText(string text)
    {
        if (!IsEditingEnabled || string.IsNullOrEmpty(text))
        {
            return;
        }

        ExecuteEdit(new InsertTextAtSelectionCommand(
            _history.Selection,
            text,
            _pendingCharacterProperties));
    }

    public void Backspace()
    {
        if (IsEditingEnabled)
        {
            ExecuteEdit(new DeleteBackwardCommand(_history.Selection));
        }
    }

    public void DeleteForward()
    {
        if (IsEditingEnabled)
        {
            ExecuteEdit(new DeleteForwardCommand(_history.Selection));
        }
    }

    public void DeleteWordBackward()
    {
        if (IsEditingEnabled)
        {
            ExecuteEdit(new DeleteWordBackwardCommand(_history.Selection));
        }
    }

    public void DeleteWordForward()
    {
        if (IsEditingEnabled)
        {
            ExecuteEdit(new DeleteWordForwardCommand(_history.Selection));
        }
    }

    public void SplitParagraph()
    {
        if (IsEditingEnabled)
        {
            ExecuteEdit(new SplitParagraphCommand(_history.Selection));
        }
    }

    public void InsertSoftBreak()
    {
        if (IsEditingEnabled)
        {
            ExecuteEdit(new InsertSoftBreakCommand(_history.Selection));
        }
    }

    [RelayCommand]
    private void ZoomIn()
    {
        ZoomMode = ZoomFitMode.Custom;
        Zoom = PageViewport.ZoomIn(Zoom);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        ZoomMode = ZoomFitMode.Custom;
        Zoom = PageViewport.ZoomOut(Zoom);
    }

    [RelayCommand]
    private void ResetZoom()
    {
        ZoomMode = ZoomFitMode.Custom;
        Zoom = 1;
    }

    [RelayCommand]
    private void FitWidth()
    {
        ZoomMode = ZoomFitMode.FitWidth;
        ApplyFit();
    }

    [RelayCommand]
    private void FitPage()
    {
        ZoomMode = ZoomFitMode.FitPage;
        ApplyFit();
    }

    public void SetViewport(double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
        {
            return;
        }

        ViewportWidth = width;
        ViewportHeight = height;

        if (ZoomMode != ZoomFitMode.Custom)
        {
            ApplyFit();
        }
    }

    internal void ApplyEdit(IEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        _history.Execute(command);
        FinishEdit();
    }

    private void ExecuteEdit(DocumentEditCommand command)
    {
        _history.Execute(command);
        FinishEdit();
    }

    private void FinishEdit()
    {
        Document = _history.Current;
        Layout = _layoutEngine.Layout(_history.Current);
        IsModified = true;
        SyncSelectionFromHistory();
        NotifyHistoryCommands();
        RefreshCharacterFormattingState();
        RefreshParagraphFormattingState();
        RefreshStyleState();
        RefreshImageState();
    }

    private void SyncSelectionFromHistory()
    {
        _updatingSelection = true;

        try
        {
            Selection = _history.Selection;
        }
        finally
        {
            _updatingSelection = false;
        }

        NotifyClipboardCommands();
        RefreshImageState();
    }

    private void NotifyHistoryCommands()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    partial void OnDocumentTitleChanged(string value) => UpdateWindowTitle();

    partial void OnIsModifiedChanged(bool value)
    {
        UpdateWindowTitle();
        OnPropertyChanged(nameof(SaveStateText));

        if (value)
        {
            StartAutosave();
        }
        else
        {
            StopAutosave();
        }
    }

    partial void OnFilePathChanged(string value) =>
        OnPropertyChanged(nameof(SaveStateText));

    partial void OnLayoutChanged(DocumentLayout? value)
    {
        CurrentPage = Math.Clamp(CurrentPage, 1, Math.Max(1, PageCount));
        OnPropertyChanged(nameof(PageCount));
        OnPropertyChanged(nameof(PageLabel));

        if (ZoomMode != ZoomFitMode.Custom)
        {
            ApplyFit();
        }
    }

    partial void OnZoomChanged(double value)
    {
        OnPropertyChanged(nameof(ZoomPercentText));

        if (!_applyingFit)
        {
            ZoomMode = ZoomFitMode.Custom;
        }
    }

    partial void OnCurrentPageChanged(int value) =>
        OnPropertyChanged(nameof(PageLabel));

    partial void OnSelectedFontFamilyChanged(string? value) =>
        OnPropertyChanged(nameof(IsFontFamilyMixed));

    partial void OnSelectedFontSizeChanged(double? value) =>
        OnPropertyChanged(nameof(IsFontSizeMixed));

    partial void OnSelectedColorHexChanged(string? value) =>
        OnPropertyChanged(nameof(IsColorMixed));

    partial void OnSelectedStyleChanged(StyleListItem? value)
    {
        if (_updatingStyles || value is null)
        {
            return;
        }

        ApplyStyle(value);
    }

    partial void OnHasSelectedImageChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditImage));
        OnPropertyChanged(nameof(CanRemoveImage));
        EditImageCommand.NotifyCanExecuteChanged();
        RemoveImageCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectionChanged(Selection value)
    {
        if (_updatingSelection)
        {
            return;
        }

        var previous = _history.Selection;
        _history.SetSelection(value);

        if (previous != _history.Selection || !value.IsCollapsed)
        {
            _pendingCharacterProperties = null;
        }

        if (_history.Selection != value)
        {
            SyncSelectionFromHistory();
        }

        NotifyClipboardCommands();
        RefreshCharacterFormattingState();
        RefreshParagraphFormattingState();
        RefreshStyleState();
        RefreshImageState();
    }

    partial void OnIsReadOnlyChanged(bool value) => NotifyEditingEnabledChanged();

    partial void OnDocumentChanged(Document value) => NotifyClipboardCommands();

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartAction));
        NotifyActionCommands();
        NotifyEditingEnabledChanged();
    }

    partial void OnIsSavingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartAction));
        OnPropertyChanged(nameof(CanCancelSave));
        OnPropertyChanged(nameof(SaveStateText));
        NotifyActionCommands();
        NotifyEditingEnabledChanged();
        CancelSaveCommand.NotifyCanExecuteChanged();
    }

    private void NotifyEditingEnabledChanged()
    {
        OnPropertyChanged(nameof(IsEditingEnabled));
        ToggleBoldCommand.NotifyCanExecuteChanged();
        ToggleItalicCommand.NotifyCanExecuteChanged();
        ToggleUnderlineCommand.NotifyCanExecuteChanged();
        ToggleStrikethroughCommand.NotifyCanExecuteChanged();
        ClearCharacterFormattingCommand.NotifyCanExecuteChanged();
        ApplyFontFamilyCommand.NotifyCanExecuteChanged();
        ApplyFontSizeCommand.NotifyCanExecuteChanged();
        ApplyColorCommand.NotifyCanExecuteChanged();
        ApplyHighlightCommand.NotifyCanExecuteChanged();
        AlignLeftCommand.NotifyCanExecuteChanged();
        AlignCenterCommand.NotifyCanExecuteChanged();
        AlignRightCommand.NotifyCanExecuteChanged();
        AlignJustifyCommand.NotifyCanExecuteChanged();
        ToggleBulletedListCommand.NotifyCanExecuteChanged();
        ToggleNumberedListCommand.NotifyCanExecuteChanged();
        IncreaseListLevelCommand.NotifyCanExecuteChanged();
        DecreaseListLevelCommand.NotifyCanExecuteChanged();
        RestartNumberingCommand.NotifyCanExecuteChanged();
        InsertPageBreakCommand.NotifyCanExecuteChanged();
        ClearParagraphFormattingCommand.NotifyCanExecuteChanged();
        ApplySpaceBeforeCommand.NotifyCanExecuteChanged();
        ApplySpaceAfterCommand.NotifyCanExecuteChanged();
        ApplyLineSpacingCommand.NotifyCanExecuteChanged();
        ApplyLeftIndentCommand.NotifyCanExecuteChanged();
        ApplyRightIndentCommand.NotifyCanExecuteChanged();
        ApplyFirstLineIndentCommand.NotifyCanExecuteChanged();
        ApplyHangingIndentCommand.NotifyCanExecuteChanged();
        NewStyleCommand.NotifyCanExecuteChanged();
        ModifyStyleCommand.NotifyCanExecuteChanged();
        SaveStyleCommand.NotifyCanExecuteChanged();
        UpdateStyleFromSelectionCommand.NotifyCanExecuteChanged();
        DeleteStyleCommand.NotifyCanExecuteChanged();
        FindCommand.NotifyCanExecuteChanged();
        InsertImageCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanEditImage));
        OnPropertyChanged(nameof(CanRemoveImage));
        EditImageCommand.NotifyCanExecuteChanged();
        RemoveImageCommand.NotifyCanExecuteChanged();
        NotifyClipboardCommands();
    }

    partial void OnErrorMessageChanged(string value) =>
        OnPropertyChanged(nameof(HasError));

    partial void OnDiagnosticsSummaryChanged(string value) =>
        OnPropertyChanged(nameof(HasDiagnostics));

    public async Task InitializeRecoveryAsync(CancellationToken cancellationToken = default)
    {
        if (_recoveryStore is null || _recoveryPrompt is null)
        {
            return;
        }

        foreach (var snapshot in _recoveryStore.ListSnapshots())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var choice = await _recoveryPrompt.PromptAsync(snapshot, cancellationToken);

            if (choice == RecoveryPromptChoice.Later)
            {
                return;
            }

            if (choice == RecoveryPromptChoice.Discard)
            {
                var confirmed = await _recoveryPrompt.ConfirmDiscardAsync(
                    snapshot,
                    cancellationToken);

                if (confirmed)
                {
                    var discardResult = await Task.Run(
                        () => _recoveryStore.Discard(snapshot, cancellationToken),
                        cancellationToken);

                    if (!discardResult.IsSuccess)
                    {
                        ShowRecoveryFailure(
                            discardResult.Diagnostics,
                            "The recovery snapshot could not be discarded.");
                    }
                }

                continue;
            }

            var loadResult = await Task.Run(
                () => _recoveryStore.Load(snapshot, cancellationToken),
                cancellationToken);

            if (!loadResult.IsSuccess || loadResult.Value is null)
            {
                ShowRecoveryFailure(
                    loadResult.Diagnostics,
                    "The recovery snapshot could not be opened.");
                continue;
            }

            ApplyRecoveredDocument(loadResult.Value);
            return;
        }
    }

    internal async Task CaptureRecoverySnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        if (_recoveryStore is null || !IsModified || IsLoading || IsSaving)
        {
            return;
        }

        if (_documentDiagnostics.Any(
                diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning))
        {
            if (!_recoveryUnavailableNoticeShown)
            {
                _recoveryUnavailableNoticeShown = true;
                var notice =
                    "Crash recovery is unavailable because this document contains "
                    + "content the exporter cannot preserve.";
                DiagnosticsSummary = string.IsNullOrWhiteSpace(DiagnosticsSummary)
                    ? notice
                    : $"{DiagnosticsSummary} {notice}";
                StatusText = "Recovery snapshot skipped for unsupported content.";
            }

            return;
        }

        var result = await Task.Run(
            () => _recoveryStore.Save(
                _history.Current,
                FilePath,
                _timeProvider.GetUtcNow(),
                cancellationToken),
            cancellationToken);

        if (result.IsSuccess)
        {
            _recoveryWarningShown = false;
            return;
        }

        if (_recoveryWarningShown)
        {
            return;
        }

        _recoveryWarningShown = true;
        ErrorMessage =
            "Could not write a recovery snapshot. Your document is still open; "
            + "save it to keep your changes.";
        StatusText = "Recovery snapshot failed. Editing is still available.";
    }

    public void Dispose()
    {
        StopAutosave();
        _saveCancellation?.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task SaveAsync(bool saveAs)
    {
        if (IsSaving)
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        _saveCancellation = cancellation;
        IsSaving = true;

        try
        {
            await SaveCoreAsync(saveAs, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Save canceled.";
        }
        finally
        {
            if (ReferenceEquals(_saveCancellation, cancellation))
            {
                _saveCancellation = null;
            }

            IsSaving = false;
        }
    }

    private async Task SaveCoreAsync(bool saveAs, CancellationToken cancellationToken)
    {
        var targetPath = FilePath;

        if (saveAs || string.IsNullOrWhiteSpace(targetPath))
        {
            var suggestedName = string.IsNullOrWhiteSpace(targetPath)
                ? BuildSuggestedFileName()
                : Path.GetFileName(targetPath);
            var pickedPath = await _filePicker.PickSaveFileAsync(
                suggestedName,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(pickedPath))
            {
                StatusText = "Save canceled.";
                return;
            }

            targetPath = pickedPath;
        }

        var extension = Path.GetExtension(targetPath);

        if (extension.Length == 0)
        {
            var addExtension = await _saveConfirmation.ConfirmAddDocxExtensionAsync(
                targetPath,
                cancellationToken);

            if (!addExtension)
            {
                StatusText = "Save canceled.";
                return;
            }

            targetPath += ".docx";
        }
        else if (!string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase))
        {
            ShowSaveFailure(
                targetPath,
                [new Diagnostic(
                    DocxDiagnosticCodes.UnsupportedExtension,
                    DiagnosticSeverity.Error,
                    "Only .docx files can be saved.")]);
            return;
        }

        var unsupportedWarnings = _documentDiagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning)
            .ToArray();

        if (unsupportedWarnings.Length > 0)
        {
            var acknowledged = await _saveConfirmation
                .ConfirmSaveDespiteUnsupportedContentAsync(
                    unsupportedWarnings,
                    cancellationToken);

            if (!acknowledged)
            {
                StatusText = "Save canceled. Unsupported content was not acknowledged.";
                return;
            }
        }

        StatusText = "Saving...";

        var result = await Task.Run(
            () => _documentSaver.Save(_history.Current, targetPath, cancellationToken),
            cancellationToken);

        if (!result.IsSuccess)
        {
            ShowSaveFailure(targetPath, result.Diagnostics);
            return;
        }

        var pathChanged = !string.Equals(FilePath, targetPath, PathComparison);

        FilePath = targetPath;

        if (pathChanged)
        {
            DocumentTitle = Path.GetFileNameWithoutExtension(targetPath);
        }

        IsModified = false;
        ErrorMessage = string.Empty;

        if (_recoveryStore is not null)
        {
            var cleanupResult = _recoveryStore.DeleteForDocument(
                _history.Current.Id,
                CancellationToken.None);
            if (!cleanupResult.IsSuccess)
            {
                DiagnosticsSummary =
                    "The recovery snapshot could not be removed after saving.";
            }
        }

        if (unsupportedWarnings.Length > 0)
        {
            _documentDiagnostics = [];
            DiagnosticsSummary = string.Empty;
            StatusText = $"Saved {Path.GetFileName(targetPath)} with acknowledged loss.";
        }
        else
        {
            StatusText = $"Saved {Path.GetFileName(targetPath)}.";
        }
    }

    private void ApplyDocument(
        Document document,
        string? filePath,
        IReadOnlyList<Diagnostic>? diagnostics = null)
    {
        _history = new CommandHistory(document, timeProvider: _timeProvider);
        _documentDiagnostics = diagnostics ?? [];
        IsReadOnly = _documentDiagnostics.Any(
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning);
        FilePath = filePath ?? string.Empty;
        DocumentTitle = document.Title;
        Document = document;
        IsModified = false;
        SyncSelectionFromHistory();
        Layout = _layoutEngine.Layout(document);
        _recoveryWarningShown = false;
        _recoveryUnavailableNoticeShown = false;
        NotifyHistoryCommands();
        RefreshCharacterFormattingState();
        RefreshParagraphFormattingState();
        RefreshStyleState();
        UpdateWindowTitle();
    }

    private void ApplyRecoveredDocument(RecoveredDocument recovered)
    {
        ApplyDocument(recovered.Document, filePath: null);
        IsModified = true;
        ErrorMessage = string.Empty;
        DiagnosticsSummary = string.Empty;

        var sourceName = string.IsNullOrWhiteSpace(recovered.SourcePath)
            ? "an unsaved document"
            : Path.GetFileName(recovered.SourcePath);
        StatusText = $"Recovered unsaved draft from {sourceName}. Save As to keep a copy.";
    }

    private void StartAutosave()
    {
        if (_recoveryStore is null || _autosaveCancellation is not null)
        {
            return;
        }

        _autosaveCancellation = new CancellationTokenSource();
        _ = RunAutosaveLoopAsync(_autosaveCancellation.Token);
    }

    private void StopAutosave()
    {
        var cancellation = _autosaveCancellation;
        _autosaveCancellation = null;

        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        cancellation.Dispose();
    }

    private async Task RunAutosaveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(_autosaveInterval, cancellationToken);
                await CaptureRecoverySnapshotAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ShowRecoveryFailure(
        IEnumerable<Diagnostic> diagnostics,
        string fallbackMessage)
    {
        var reason = diagnostics
            .FirstOrDefault(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            ?.Message
            ?? fallbackMessage;

        ErrorMessage = reason;
        StatusText = "Recovery snapshot action failed.";
    }

    private void UpdateWindowTitle()
    {
        var modifiedMarker = IsModified ? "*" : string.Empty;
        WindowTitle = $"{DocumentTitle}{modifiedMarker} - MicroslopWords";
    }

    private void NotifyActionCommands()
    {
        NewDocumentCommand.NotifyCanExecuteChanged();
        OpenDocumentCommand.NotifyCanExecuteChanged();
        SaveDocumentCommand.NotifyCanExecuteChanged();
        SaveDocumentAsCommand.NotifyCanExecuteChanged();
    }

    private string BuildSuggestedFileName()
    {
        var name = string.IsNullOrWhiteSpace(DocumentTitle) ? "Untitled" : DocumentTitle;
        return $"{name}.docx";
    }

    private void ApplyFit()
    {
        if (Layout is null || ViewportWidth <= 0 || ViewportHeight <= 0)
        {
            return;
        }

        _applyingFit = true;

        try
        {
            Zoom = ZoomMode == ZoomFitMode.FitPage
                ? PageViewport.ZoomForFitPage(
                    ViewportWidth,
                    ViewportHeight,
                    Layout.PageWidthPoints,
                    Layout.PageHeightPoints,
                    PageViewport.OuterMarginDips,
                    PageViewport.OuterMarginDips)
                : PageViewport.ZoomForFitWidth(
                    ViewportWidth,
                    Layout.PageWidthPoints,
                    PageViewport.OuterMarginDips);
        }
        finally
        {
            _applyingFit = false;
        }
    }

    private enum CharacterProperty
    {
        Bold,
        Italic,
        Underline,
        Strikethrough,
    }

    private void ToggleCharacterProperty(CharacterProperty property)
    {
        var properties = GetSelectedRunProperties();
        var current = CommonBoolean(properties.Select(run => GetProperty(run, property)));
        var value = current != true;

        ApplyCharacterProperties(run => SetProperty(run, property, value));
    }

    private void ApplyCharacterProperties(Func<RunProperties, RunProperties> transform)
    {
        if (!IsEditingEnabled)
        {
            return;
        }

        ErrorMessage = string.Empty;

        if (_history.Selection.IsCollapsed)
        {
            var effective = EffectivePropertiesAtCaret();
            var transformed = transform(effective);
            var direct = _pendingCharacterProperties
                ?? new RunProperties { StyleId = effective.StyleId };
            _pendingCharacterProperties = StyleResolver.MergeOverrides(
                direct,
                effective,
                transformed);
            RefreshCharacterFormattingState();
            return;
        }

        ExecuteEdit(new FormatSelectionCommand(_history.Selection, transform));
        _pendingCharacterProperties = null;
        RefreshCharacterFormattingState();
    }

    private void RefreshCharacterFormattingState()
    {
        var properties = GetSelectedRunProperties();

        if (properties.Count == 0)
        {
            properties.Add(RunProperties.Default);
        }

        IsBoldSelected = CommonBoolean(properties.Select(run => run.Bold));
        IsItalicSelected = CommonBoolean(properties.Select(run => run.Italic));
        IsUnderlineSelected = CommonBoolean(properties.Select(run => run.Underline));
        IsStrikethroughSelected = CommonBoolean(properties.Select(run => run.Strikethrough));

        var families = properties
            .Select(run => run.FontFamily)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        SelectedFontFamily = families.Length == 1 ? families[0] : null;

        var sizes = properties
            .Select(run => run.FontSizePoints)
            .Distinct()
            .ToArray();
        SelectedFontSize = sizes.Length == 1 ? sizes[0] : null;

        var colors = properties
            .Select(run => run.ColorHex)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        SelectedColorHex = colors.Length == 1 ? colors[0] : null;

        var highlights = properties
            .Select(run => run.HighlightHex)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        SelectedHighlightHex = highlights.Length == 1 ? highlights[0] : null;
        IsHighlightMixed = highlights.Length > 1;

        if (SelectedFontFamily is not null)
        {
            FontFamilyInput = SelectedFontFamily;
        }

        if (SelectedFontSize is { } size)
        {
            FontSizeInput = size.ToString(CultureInfo.InvariantCulture);
        }

        if (SelectedColorHex is not null)
        {
            ColorInput = SelectedColorHex;
        }

        HighlightInput = SelectedHighlightHex ?? string.Empty;
    }

    private List<RunProperties> GetSelectedRunProperties()
    {
        if (Document is null)
        {
            return [RunProperties.Default];
        }

        if (_history.Selection.IsCollapsed)
        {
            return [EffectivePropertiesAtCaret()];
        }

        var selection = SelectionMap.Normalize(_history.Selection, Document);
        var start = selection.GetStart(Document);
        var end = selection.GetEnd(Document);
        var paragraphs = Document.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .ToList();
        var startIndex = FindParagraphIndex(paragraphs, start.ParagraphId);
        var endIndex = FindParagraphIndex(paragraphs, end.ParagraphId);

        if (startIndex < 0 || endIndex < 0)
        {
            return [RunProperties.Default];
        }

        var result = new List<RunProperties>();

        for (var index = startIndex; index <= endIndex; index++)
        {
            var paragraph = paragraphs[index];
            var selectionStart = index == startIndex
                ? GetParagraphOffset(paragraph, start)
                : 0;
            var selectionEnd = index == endIndex
                ? GetParagraphOffset(paragraph, end)
                : paragraph.PlainText.Length;
            var runStart = 0;

            foreach (var run in paragraph.Runs)
            {
                var runEnd = runStart + run.Text.Length;

                if (runEnd > selectionStart && runStart < selectionEnd)
                {
                    result.Add(StyleResolver.ResolveRun(
                        run,
                        paragraph,
                        Document.EffectiveStyles));
                }

                runStart = runEnd;
            }
        }

        return result;
    }

    private RunProperties EffectivePropertiesAtCaret()
    {
        if (Document is null)
        {
            return RunProperties.Default;
        }

        var selection = SelectionMap.Normalize(_history.Selection, Document);
        var paragraph = Document.FindParagraph(selection.Focus.ParagraphId);

        if (paragraph is null)
        {
            return _pendingCharacterProperties ?? RunProperties.Default;
        }

        if (_pendingCharacterProperties is not null)
        {
            return StyleResolver.ResolveRunProperties(
                _pendingCharacterProperties,
                paragraph,
                Document.EffectiveStyles);
        }

        if (paragraph.Runs.Length == 0)
        {
            return RunProperties.Default;
        }

        var run = paragraph.Runs
            .FirstOrDefault(candidate => candidate.Id == selection.Focus.RunId)
            ?? paragraph.Runs[0];

        return StyleResolver.ResolveRun(
            run,
            paragraph,
            Document.EffectiveStyles);
    }

    private static int GetParagraphOffset(ParagraphBlock paragraph, TextPosition position) =>
        TextNavigation.GetParagraphTextOffset(
            paragraph,
            position.RunId,
            position.Offset);

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

    private static bool? CommonBoolean(IEnumerable<bool> values)
    {
        var list = values.ToArray();

        if (list.Length == 0)
        {
            return false;
        }

        return list.All(value => value)
            ? true
            : list.Any(value => value)
                ? null
                : false;
    }

    private static bool GetProperty(RunProperties properties, CharacterProperty property) =>
        property switch
        {
            CharacterProperty.Bold => properties.Bold,
            CharacterProperty.Italic => properties.Italic,
            CharacterProperty.Underline => properties.Underline,
            CharacterProperty.Strikethrough => properties.Strikethrough,
            _ => false,
        };

    private static RunProperties SetProperty(
        RunProperties properties,
        CharacterProperty property,
        bool value) =>
        property switch
        {
            CharacterProperty.Bold => properties with { Bold = value },
            CharacterProperty.Italic => properties with { Italic = value },
            CharacterProperty.Underline => properties with { Underline = value },
            CharacterProperty.Strikethrough => properties with { Strikethrough = value },
            _ => properties,
        };

    private static bool TryNormalizeColor(string input, out string color)
    {
        var trimmed = input.Trim();

        if (!trimmed.StartsWith('#'))
        {
            trimmed = "#" + trimmed;
        }

        if (trimmed.Length == 7
            && trimmed[1..].All(character => Uri.IsHexDigit(character)))
        {
            color = trimmed.ToUpperInvariant();
            return true;
        }

        color = string.Empty;
        return false;
    }

    private bool IsStyleAlreadyApplied(StyleListItem style)
    {
        var sheet = Document.EffectiveStyles;

        if (string.Equals(style.Kind, "Paragraph", StringComparison.Ordinal))
        {
            var paragraphs = GetSelectedParagraphs();

            return paragraphs.Count > 0
                && paragraphs.All(paragraph => string.Equals(
                    StyleResolver.ResolveParagraph(paragraph, sheet).StyleId
                        ?? StyleSheet.NormalStyleId,
                    style.Id,
                    StringComparison.Ordinal));
        }

        var properties = GetSelectedRunProperties();

        return properties.Count > 0
            && properties.All(run => string.Equals(
                run.StyleId ?? StyleSheet.DefaultCharacterStyleId,
                style.Id,
                StringComparison.Ordinal));
    }

    private void RefreshStyleState()
    {
        var sheet = Document.EffectiveStyles;
        var paragraphs = GetSelectedParagraphs();
        var paragraphStyleIds = paragraphs
            .Select(paragraph => StyleResolver.ResolveParagraph(paragraph, sheet).StyleId
                ?? StyleSheet.NormalStyleId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var paragraphStyleMixed = paragraphStyleIds.Length > 1;
        var currentParagraphStyleId = paragraphStyleIds.Length == 1
            ? paragraphStyleIds[0]
            : null;
        var currentParagraphStyle = sheet.FindParagraphStyle(currentParagraphStyleId)
            ?? sheet.FindParagraphStyle(StyleSheet.NormalStyleId);
        CurrentParagraphStyleName = paragraphStyleMixed
            ? "Mixed"
            : currentParagraphStyle?.Name ?? "Normal";

        var runStyleIds = GetSelectedRunProperties()
            .Select(properties => properties.StyleId ?? StyleSheet.DefaultCharacterStyleId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var runStyleMixed = runStyleIds.Length > 1;
        var currentCharacterStyleId = runStyleIds.Length == 1
            ? runStyleIds[0]
            : null;
        var currentCharacterStyle = sheet.FindCharacterStyle(currentCharacterStyleId)
            ?? sheet.FindCharacterStyle(StyleSheet.DefaultCharacterStyleId);
        CurrentCharacterStyleName = runStyleMixed
            ? "Mixed"
            : currentCharacterStyle?.Name ?? "Default";
        IsStyleMixed = paragraphStyleMixed || runStyleMixed;

        _updatingStyles = true;

        try
        {
            var items = new List<StyleListItem>();

            foreach (var style in sheet.ParagraphStyles)
            {
                items.Add(StyleListItem.FromParagraphStyle(
                    style,
                    string.Equals(style.Id, currentParagraphStyleId, StringComparison.Ordinal)));
            }

            foreach (var style in sheet.CharacterStyles)
            {
                items.Add(StyleListItem.FromCharacterStyle(
                    style,
                    string.Equals(style.Id, currentCharacterStyleId, StringComparison.Ordinal)));
            }

            StyleItems = items;
            SelectedStyle = items.FirstOrDefault(item =>
                string.Equals(item.Kind, "Paragraph", StringComparison.Ordinal)
                && string.Equals(item.Id, currentParagraphStyleId, StringComparison.Ordinal));
        }
        finally
        {
            _updatingStyles = false;
        }
    }

    private void RefreshImageState()
    {
        var selected = ImageSelection.FindSelectedImage(Document, _history.Selection);

        if (selected is null)
        {
            _selectedImageParagraphId = null;
            _selectedImageRunId = null;
            HasSelectedImage = false;
            SelectedImageDescription = string.Empty;
            return;
        }

        _selectedImageParagraphId = selected.Value.ParagraphId;
        _selectedImageRunId = selected.Value.RunId;
        HasSelectedImage = true;
        SelectedImageDescription =
            $"{selected.Value.Image.AccessibleName} - "
            + $"{selected.Value.Image.WidthPoints:0.#} x "
            + $"{selected.Value.Image.HeightPoints:0.#} pt";
    }

    private void RefreshParagraphFormattingState()
    {
        var paragraphs = GetSelectedParagraphs();
        var sheet = Document.EffectiveStyles;
        var properties = paragraphs
            .Select(paragraph => StyleResolver.ResolveParagraph(paragraph, sheet))
            .ToList();

        if (properties.Count == 0)
        {
            properties.Add(ParagraphProperties.Default);
        }

        SelectedAlignment = CommonValue(properties.Select(item => item.Alignment));
        SelectedSpaceBefore = CommonValue(properties.Select(item => item.SpaceBeforePoints));
        SelectedSpaceAfter = CommonValue(properties.Select(item => item.SpaceAfterPoints));
        SelectedLineSpacing = CommonValue(properties.Select(item => item.LineSpacing));
        SelectedLeftIndent = CommonValue(properties.Select(item => item.LeftIndentPoints));
        SelectedRightIndent = CommonValue(properties.Select(item => item.RightIndentPoints));

        var firstLineIndents = properties
            .Select(item => item.FirstLineIndentPoints)
            .Distinct()
            .ToArray();

        if (firstLineIndents.Length == 1)
        {
            var firstLine = firstLineIndents[0];
            SelectedFirstLineIndent = Math.Max(0, firstLine);
            SelectedHangingIndent = Math.Max(0, -firstLine);
        }
        else
        {
            SelectedFirstLineIndent = null;
            SelectedHangingIndent = null;
        }

        var kinds = properties.Select(item => item.ListKind).ToArray();
        IsBulletedList = kinds.All(kind => kind == ListKind.Bulleted)
            ? true
            : kinds.All(kind => kind != ListKind.Bulleted)
                ? false
                : null;
        IsNumberedList = kinds.All(kind => kind == ListKind.Numbered)
            ? true
            : kinds.All(kind => kind != ListKind.Numbered)
                ? false
                : null;

        var levels = properties
            .Where(item => item.ListKind != ListKind.None)
            .Select(item => item.ListLevel)
            .Distinct()
            .ToArray();
        SelectedListLevel = levels.Length == 1 ? levels[0] : null;

        if (SelectedSpaceBefore is { } spaceBefore)
        {
            SpaceBeforeInput = spaceBefore.ToString(CultureInfo.InvariantCulture);
        }

        if (SelectedSpaceAfter is { } spaceAfter)
        {
            SpaceAfterInput = spaceAfter.ToString(CultureInfo.InvariantCulture);
        }

        if (SelectedLineSpacing is { } lineSpacing)
        {
            LineSpacingInput = lineSpacing.ToString(CultureInfo.InvariantCulture);
        }

        if (SelectedLeftIndent is { } left)
        {
            LeftIndentInput = left.ToString(CultureInfo.InvariantCulture);
        }

        if (SelectedRightIndent is { } right)
        {
            RightIndentInput = right.ToString(CultureInfo.InvariantCulture);
        }

        if (SelectedFirstLineIndent is { } first)
        {
            FirstLineIndentInput = first.ToString(CultureInfo.InvariantCulture);
        }

        if (SelectedHangingIndent is { } hanging)
        {
            HangingIndentInput = hanging.ToString(CultureInfo.InvariantCulture);
        }

        OnPropertyChanged(nameof(IsAlignmentMixed));
        OnPropertyChanged(nameof(AreParagraphValuesMixed));
    }

    private List<ParagraphBlock> GetSelectedParagraphs()
    {
        if (Document is null)
        {
            return [];
        }

        var selection = SelectionMap.Normalize(_history.Selection, Document);
        var paragraphs = Document.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .ToList();

        if (selection.IsCollapsed)
        {
            var current = Document.FindParagraph(selection.Focus.ParagraphId);
            return current is null ? [] : [current];
        }

        var start = selection.GetStart(Document);
        var end = selection.GetEnd(Document);
        var startIndex = FindParagraphIndex(paragraphs, start.ParagraphId);
        var endIndex = FindParagraphIndex(paragraphs, end.ParagraphId);

        if (startIndex < 0 || endIndex < 0)
        {
            return [];
        }

        if (startIndex > endIndex)
        {
            (startIndex, endIndex) = (endIndex, startIndex);
        }

        return paragraphs
            .Skip(startIndex)
            .Take(endIndex - startIndex + 1)
            .ToList();
    }

    private static T? CommonValue<T>(IEnumerable<T> values)
        where T : struct
    {
        var distinct = values.Distinct().Take(2).ToArray();

        return distinct.Length == 1 ? distinct[0] : null;
    }

    private static bool TryParsePointValue(
        string input,
        out double value,
        bool allowNegative)
    {
        if (!double.TryParse(
                input,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value))
        {
            return false;
        }

        return allowNegative || value >= 0;
    }
    private void ClearMessages()
    {
        ErrorMessage = string.Empty;
        DiagnosticsSummary = string.Empty;
    }

    private void ShowOpenFailure(string path, IEnumerable<Diagnostic> diagnostics)
    {
        var fileName = string.IsNullOrWhiteSpace(path)
            ? "the selected file"
            : Path.GetFileName(path);
        var reason = diagnostics
            .FirstOrDefault(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            ?.Message
            ?? "The file could not be opened.";

        DiagnosticsSummary = string.Empty;
        ErrorMessage = $"Could not open {fileName}: {reason}";
        StatusText = "Open failed.";
    }

    private void ShowSaveFailure(string path, IEnumerable<Diagnostic> diagnostics)
    {
        var fileName = string.IsNullOrWhiteSpace(path)
            ? "the document"
            : Path.GetFileName(path);
        var reason = diagnostics
            .FirstOrDefault(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            ?.Message
            ?? "The document could not be saved.";

        ErrorMessage = $"Could not save {fileName}: {reason}";
        StatusText = "Save failed. The document is still unsaved.";
    }

    private async Task ShowProgressAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            IsProgressVisible = true;
        }
        catch (OperationCanceledException)
        {
        }
    }
}
