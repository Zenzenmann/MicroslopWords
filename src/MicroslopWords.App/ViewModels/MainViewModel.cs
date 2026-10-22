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
    private readonly IDocxFilePicker _filePicker;
    private readonly IDocxDocumentLoader _documentLoader;
    private readonly LayoutEngine _layoutEngine;
    private readonly bool _isReadOnly = true;
    private CommandHistory _history;
    private int _documentGeneration;

    public MainViewModel()
        : this(new UnavailableDocxFilePicker(), new DocxDocumentLoader(), new LayoutEngine())
    {
    }

    public MainViewModel(
        IDocxFilePicker filePicker,
        IDocxDocumentLoader documentLoader,
        LayoutEngine layoutEngine)
    {
        ArgumentNullException.ThrowIfNull(filePicker);
        ArgumentNullException.ThrowIfNull(documentLoader);
        ArgumentNullException.ThrowIfNull(layoutEngine);

        _filePicker = filePicker;
        _documentLoader = documentLoader;
        _layoutEngine = layoutEngine;
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
    public partial DocumentLayout? Layout { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsProgressVisible { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DiagnosticsSummary { get; set; } = string.Empty;

    public bool IsReadOnly => _isReadOnly;

    public bool IsEditingEnabled => !_isReadOnly;

    public bool CanStartAction => !IsLoading;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasDiagnostics => !string.IsNullOrEmpty(DiagnosticsSummary);

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

            ApplyDocument(result.Value, path);

            var warnings = result.Diagnostics
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning)
                .ToArray();

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

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void SaveDocument() => StatusText = "Saving is planned for STORY-002.";

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ToggleBold() => StatusText = "Bold is planned for STORY-007.";

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ToggleItalic() => StatusText = "Italic is planned for STORY-007.";

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void ToggleUnderline() => StatusText = "Underline is planned for STORY-007.";

    [RelayCommand(CanExecute = nameof(IsEditingEnabled))]
    private void Find() => StatusText = "Find is planned for STORY-008.";

    partial void OnDocumentTitleChanged(string value) => UpdateWindowTitle();

    partial void OnIsModifiedChanged(bool value) => UpdateWindowTitle();

    partial void OnLayoutChanged(DocumentLayout? value)
    {
        OnPropertyChanged(nameof(PageCount));
        OnPropertyChanged(nameof(PageLabel));
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartAction));
        NewDocumentCommand.NotifyCanExecuteChanged();
        OpenDocumentCommand.NotifyCanExecuteChanged();
    }

    partial void OnErrorMessageChanged(string value) =>
        OnPropertyChanged(nameof(HasError));

    partial void OnDiagnosticsSummaryChanged(string value) =>
        OnPropertyChanged(nameof(HasDiagnostics));

    private void ApplyDocument(Document document, string? filePath)
    {
        _history = new CommandHistory(document);
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
