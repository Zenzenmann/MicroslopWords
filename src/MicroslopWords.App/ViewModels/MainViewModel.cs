using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicroslopWords.App.Services;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using MicroslopWords.Formats.Docx;
using MicroslopWords.Layout;

namespace MicroslopWords.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private readonly IDocxFilePicker _filePicker;
    private readonly IDocxDocumentLoader _documentLoader;
    private readonly IDocxDocumentSaver _documentSaver;
    private readonly ISaveConfirmation _saveConfirmation;
    private readonly LayoutEngine _layoutEngine;
    private readonly bool _isReadOnly = true;
    private CommandHistory _history;
    private IReadOnlyList<Diagnostic> _documentDiagnostics = [];
    private CancellationTokenSource? _saveCancellation;
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
    {
        ArgumentNullException.ThrowIfNull(filePicker);
        ArgumentNullException.ThrowIfNull(documentLoader);
        ArgumentNullException.ThrowIfNull(layoutEngine);
        ArgumentNullException.ThrowIfNull(documentSaver);
        ArgumentNullException.ThrowIfNull(saveConfirmation);

        _filePicker = filePicker;
        _documentLoader = documentLoader;
        _layoutEngine = layoutEngine;
        _documentSaver = documentSaver;
        _saveConfirmation = saveConfirmation;
        _history = new CommandHistory(Document.CreateEmpty());

        ApplyDocument(_history.Current, filePath: null);
        StatusText = "Ready.";
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
        UpdateWindowTitle();
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
