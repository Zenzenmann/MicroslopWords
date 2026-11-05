using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicroslopWords.App.Controls;
using MicroslopWords.App.Services;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
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
    private readonly LayoutEngine _layoutEngine;
    private CommandHistory _history;
    private IReadOnlyList<Diagnostic> _documentDiagnostics = [];
    private CancellationTokenSource? _saveCancellation;
    private CancellationTokenSource? _autosaveCancellation;
    private bool _recoveryWarningShown;
    private bool _recoveryUnavailableNoticeShown;
    private bool _applyingFit;
    private bool _updatingSelection;
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
        IClipboardService? clipboardService = null)
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
    public partial double Zoom { get; set; } = 1.0;

    [ObservableProperty]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    public partial ZoomFitMode ZoomMode { get; set; } = ZoomFitMode.Custom;

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
    private void ToggleBold() => StatusText = "Bold is planned for STORY-007.";

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ToggleItalic() => StatusText = "Italic is planned for STORY-007.";

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ToggleUnderline() => StatusText = "Underline is planned for STORY-007.";

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void Find() => StatusText = "Find is planned for STORY-008.";

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

        StatusText = content.HasImage
            ? "Image paste is not available yet."
            : "Clipboard is empty.";
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

        ExecuteEdit(new InsertTextAtSelectionCommand(_history.Selection, text));
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

    partial void OnSelectionChanged(Selection value)
    {
        if (_updatingSelection)
        {
            return;
        }

        _history.SetSelection(value);

        if (_history.Selection != value)
        {
            SyncSelectionFromHistory();
        }

        NotifyClipboardCommands();
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
        FindCommand.NotifyCanExecuteChanged();
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
