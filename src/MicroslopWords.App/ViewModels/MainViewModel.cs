using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicroslopWords.App.Services;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using MicroslopWords.Formats.Docx;
using MicroslopWords.Layout;

namespace MicroslopWords.App.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
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
    private readonly LayoutEngine _layoutEngine;
    private readonly bool _isReadOnly = true;
    private CommandHistory _history;
    private IReadOnlyList<Diagnostic> _documentDiagnostics = [];
    private CancellationTokenSource? _saveCancellation;
    private CancellationTokenSource? _autosaveCancellation;
    private bool _recoveryWarningShown;
    private bool _recoveryUnavailableNoticeShown;
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
        IRecoveryPrompt recoveryPrompt)
        : this(
            filePicker,
            documentLoader,
            layoutEngine,
            documentSaver,
            saveConfirmation,
            recoveryStore,
            recoveryPrompt,
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
        TimeProvider timeProvider)
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
        _history = new CommandHistory(Document.CreateEmpty());

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

    public bool IsReadOnly => _isReadOnly;

    public bool IsEditingEnabled => !_isReadOnly;

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

    public string PageLabel => $"Page {PageCount}";

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

    internal void ApplyEdit(IEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var document = _history.Execute(command);
        IsModified = true;
        Layout = _layoutEngine.Layout(document);
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
        OnPropertyChanged(nameof(PageCount));
        OnPropertyChanged(nameof(PageLabel));
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartAction));
        NotifyActionCommands();
    }

    partial void OnIsSavingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartAction));
        OnPropertyChanged(nameof(CanCancelSave));
        OnPropertyChanged(nameof(SaveStateText));
        NotifyActionCommands();
        CancelSaveCommand.NotifyCanExecuteChanged();
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
        _history = new CommandHistory(document);
        _documentDiagnostics = diagnostics ?? [];
        FilePath = filePath ?? string.Empty;
        DocumentTitle = document.Title;
        IsModified = false;
        Layout = _layoutEngine.Layout(document);
        _recoveryWarningShown = false;
        _recoveryUnavailableNoticeShown = false;
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
