using System.Collections.Immutable;
using MicroslopWords.App.Services;
using MicroslopWords.App.ViewModels;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using MicroslopWords.Formats.Docx;
using MicroslopWords.Layout;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void New_document_is_read_only_with_one_page()
    {
        var viewModel = CreateViewModel();

        viewModel.DocumentTitle.ShouldBe("Untitled");
        viewModel.WindowTitle.ShouldBe("Untitled - MicroslopWords");
        viewModel.FilePath.ShouldBeEmpty();
        viewModel.PageCount.ShouldBe(1);
        viewModel.PageLabel.ShouldBe("Page 1");
        viewModel.IsModified.ShouldBeFalse();
        viewModel.IsReadOnly.ShouldBeTrue();
        viewModel.IsEditingEnabled.ShouldBeFalse();
        viewModel.SaveStateText.ShouldBe("Not saved");
    }

    [Fact]
    public async Task Open_success_updates_document_state_and_diagnostics()
    {
        var picker = new FakeFilePicker(@"C:\docs\opened.docx");
        var loader = new FakeDocumentLoader((_, _) =>
            OperationResult.Success(
                CreateDocument("Opened"),
                [new Diagnostic("MSW2001", DiagnosticSeverity.Warning, "Tables are not shown.")]));
        var viewModel = new MainViewModel(picker, loader, new LayoutEngine());

        await viewModel.OpenDocumentCommand.ExecuteAsync(null);

        loader.LastPath.ShouldBe(@"C:\docs\opened.docx");
        viewModel.DocumentTitle.ShouldBe("Opened");
        viewModel.WindowTitle.ShouldBe("Opened - MicroslopWords");
        viewModel.FilePath.ShouldBe(@"C:\docs\opened.docx");
        viewModel.PageCount.ShouldBe(1);
        viewModel.HasDiagnostics.ShouldBeTrue();
        viewModel.HasError.ShouldBeFalse();
        viewModel.StatusText.ShouldContain("unsupported");
    }

    [Fact]
    public async Task Open_failure_keeps_previous_document_and_reports_file()
    {
        var picker = new FakeFilePicker(@"C:\docs\broken.docx");
        var loader = new FakeDocumentLoader((_, _) =>
            OperationResult.Failure<Document>(
                new Diagnostic(
                    "MSW1001",
                    DiagnosticSeverity.Error,
                    "The selected file is not a readable DOCX package.")));
        var viewModel = new MainViewModel(picker, loader, new LayoutEngine());
        var previousDocument = viewModel.History.Current;

        await viewModel.OpenDocumentCommand.ExecuteAsync(null);

        viewModel.History.Current.ShouldBeSameAs(previousDocument);
        viewModel.HasError.ShouldBeTrue();
        viewModel.ErrorMessage.ShouldContain("broken.docx");
        viewModel.ErrorMessage.ShouldContain("not a readable DOCX");
        viewModel.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task Open_shows_progress_after_one_second_and_keeps_previous_document_visible()
    {
        var release = new ManualResetEventSlim();
        var picker = new FakeFilePicker(@"C:\docs\slow.docx");
        var loader = new FakeDocumentLoader((_, cancellationToken) =>
        {
            release.Wait(TimeSpan.FromSeconds(10), cancellationToken);
            return OperationResult.Success(CreateDocument("Slow"));
        });
        var viewModel = new MainViewModel(picker, loader, new LayoutEngine());
        var previousDocument = viewModel.History.Current;

        var openTask = viewModel.OpenDocumentCommand.ExecuteAsync(null);

        await Task.Delay(
            TimeSpan.FromMilliseconds(1200),
            TestContext.Current.CancellationToken);

        viewModel.IsProgressVisible.ShouldBeTrue();
        viewModel.IsLoading.ShouldBeTrue();
        viewModel.CanStartAction.ShouldBeFalse();
        viewModel.History.Current.ShouldBeSameAs(previousDocument);

        release.Set();
        await openTask;

        viewModel.IsProgressVisible.ShouldBeFalse();
        viewModel.IsLoading.ShouldBeFalse();
        viewModel.DocumentTitle.ShouldBe("Slow");
    }

    [Fact]
    public async Task Canceling_open_discards_pending_result()
    {
        var started = new ManualResetEventSlim();
        var picker = new FakeFilePicker(@"C:\docs\slow.docx");
        var loader = new FakeDocumentLoader((_, cancellationToken) =>
        {
            started.Set();
            cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
            cancellationToken.ThrowIfCancellationRequested();
            return OperationResult.Success(CreateDocument("Slow"));
        });
        var viewModel = new MainViewModel(picker, loader, new LayoutEngine());
        var previousDocument = viewModel.History.Current;

        var openTask = viewModel.OpenDocumentCommand.ExecuteAsync(null);
        started
            .Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)
            .ShouldBeTrue();

        viewModel.OpenDocumentCancelCommand.Execute(null);
        await openTask;

        viewModel.History.Current.ShouldBeSameAs(previousDocument);
        viewModel.DocumentTitle.ShouldBe("Untitled");
        viewModel.IsLoading.ShouldBeFalse();
        viewModel.IsProgressVisible.ShouldBeFalse();
        viewModel.StatusText.ShouldBe("Open canceled.");
    }

    [Fact]
    public async Task Save_with_existing_path_writes_to_current_path_and_clears_dirty_state()
    {
        var picker = new FakeFilePicker(@"C:\docs\source.docx");
        var loader = new FakeDocumentLoader((_, _) =>
            OperationResult.Success(CreateDocument("Opened")));
        var saver = new FakeDocumentSaver((_, path) => OperationResult.Success(path));
        var viewModel = new MainViewModel(
            picker,
            loader,
            new LayoutEngine(),
            saver,
            new FakeSaveConfirmation());

        await viewModel.OpenDocumentCommand.ExecuteAsync(null);
        AddEdit(viewModel);
        var history = viewModel.History;

        viewModel.IsModified.ShouldBeTrue();
        viewModel.WindowTitle.ShouldBe("Opened* - MicroslopWords");
        viewModel.SaveStateText.ShouldBe("Unsaved changes");

        await viewModel.SaveDocumentCommand.ExecuteAsync(null);

        saver.SaveCount.ShouldBe(1);
        saver.LastPath.ShouldBe(@"C:\docs\source.docx");
        picker.SavePickerCalls.ShouldBe(0);
        viewModel.FilePath.ShouldBe(@"C:\docs\source.docx");
        viewModel.IsModified.ShouldBeFalse();
        viewModel.WindowTitle.ShouldBe("Opened - MicroslopWords");
        viewModel.SaveStateText.ShouldBe("Saved");
        viewModel.History.ShouldBeSameAs(history);
        viewModel.History.CanUndo.ShouldBeTrue();
        viewModel.StatusText.ShouldBe("Saved source.docx.");
    }

    [Fact]
    public async Task Save_without_path_invokes_save_as_and_changes_path_after_success()
    {
        var picker = new FakeFilePicker(openPath: null, savePath: @"C:\docs\new.docx");
        var saver = new FakeDocumentSaver((_, path) => OperationResult.Success(path));
        var viewModel = new MainViewModel(
            picker,
            new FakeDocumentLoader((_, _) =>
                OperationResult.Failure<Document>("TEST", "Not used.")),
            new LayoutEngine(),
            saver,
            new FakeSaveConfirmation());

        AddEdit(viewModel);

        await viewModel.SaveDocumentCommand.ExecuteAsync(null);

        picker.LastSuggestedSaveName.ShouldBe("Untitled.docx");
        saver.LastPath.ShouldBe(@"C:\docs\new.docx");
        viewModel.FilePath.ShouldBe(@"C:\docs\new.docx");
        viewModel.DocumentTitle.ShouldBe("new");
        viewModel.WindowTitle.ShouldBe("new - MicroslopWords");
        viewModel.IsModified.ShouldBeFalse();
        viewModel.SaveStateText.ShouldBe("Saved");
    }

    [Fact]
    public async Task Canceled_save_as_keeps_path_content_dirty_state_and_history()
    {
        var picker = new FakeFilePicker(
            openPath: @"C:\docs\source.docx",
            savePath: null);
        var loader = new FakeDocumentLoader((_, _) =>
            OperationResult.Success(CreateDocument("Opened")));
        var saver = new FakeDocumentSaver((_, path) => OperationResult.Success(path));
        var viewModel = new MainViewModel(
            picker,
            loader,
            new LayoutEngine(),
            saver,
            new FakeSaveConfirmation());

        await viewModel.OpenDocumentCommand.ExecuteAsync(null);
        AddEdit(viewModel);
        var history = viewModel.History;

        await viewModel.SaveDocumentAsCommand.ExecuteAsync(null);

        saver.SaveCount.ShouldBe(0);
        viewModel.FilePath.ShouldBe(@"C:\docs\source.docx");
        viewModel.DocumentTitle.ShouldBe("Opened");
        viewModel.IsModified.ShouldBeTrue();
        viewModel.History.ShouldBeSameAs(history);
        viewModel.StatusText.ShouldBe("Save canceled.");
    }

    [Fact]
    public async Task Failed_save_as_keeps_previous_path_and_file_state()
    {
        var picker = new FakeFilePicker(
            openPath: @"C:\docs\source.docx",
            savePath: @"C:\docs\unwritable.docx");
        var loader = new FakeDocumentLoader((_, _) =>
            OperationResult.Success(CreateDocument("Opened")));
        var saver = new FakeDocumentSaver((_, _) =>
            OperationResult.Failure<string>(
                new Diagnostic(
                    DocxDiagnosticCodes.SaveFailed,
                    DiagnosticSeverity.Error,
                    "The document could not be saved.")));
        var viewModel = new MainViewModel(
            picker,
            loader,
            new LayoutEngine(),
            saver,
            new FakeSaveConfirmation());

        await viewModel.OpenDocumentCommand.ExecuteAsync(null);
        AddEdit(viewModel);
        var history = viewModel.History;

        await viewModel.SaveDocumentAsCommand.ExecuteAsync(null);

        viewModel.FilePath.ShouldBe(@"C:\docs\source.docx");
        viewModel.IsModified.ShouldBeTrue();
        viewModel.History.ShouldBeSameAs(history);
        viewModel.HasError.ShouldBeTrue();
        viewModel.ErrorMessage.ShouldContain("unwritable.docx");
        viewModel.StatusText.ShouldContain("still unsaved");
    }

    [Fact]
    public async Task Canceled_save_keeps_document_dirty_and_path_unchanged()
    {
        var picker = new FakeFilePicker(@"C:\docs\source.docx");
        var loader = new FakeDocumentLoader((_, _) =>
            OperationResult.Success(CreateDocument("Opened")));
        var saver = new CancellableDocumentSaver();
        var viewModel = new MainViewModel(
            picker,
            loader,
            new LayoutEngine(),
            saver,
            new FakeSaveConfirmation());

        await viewModel.OpenDocumentCommand.ExecuteAsync(null);
        AddEdit(viewModel);
        var history = viewModel.History;

        var saveTask = viewModel.SaveDocumentCommand.ExecuteAsync(null);
        saver.Started
            .Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)
            .ShouldBeTrue();

        viewModel.IsSaving.ShouldBeTrue();
        viewModel.CancelSaveCommand.Execute(null);
        await saveTask;

        viewModel.IsSaving.ShouldBeFalse();
        viewModel.FilePath.ShouldBe(@"C:\docs\source.docx");
        viewModel.IsModified.ShouldBeTrue();
        viewModel.History.ShouldBeSameAs(history);
        viewModel.StatusText.ShouldBe("Save canceled.");
    }

    [Fact]
    public async Task Unsupported_content_save_defaults_to_cancel_without_writing()
    {
        var picker = new FakeFilePicker(@"C:\docs\source.docx");
        var loader = new FakeDocumentLoader((_, _) =>
            OperationResult.Success(
                CreateDocument("Opened"),
                [new Diagnostic(
                    DocxDiagnosticCodes.UnsupportedTable,
                    DiagnosticSeverity.Warning,
                    "Tables are not shown.")]));
        var saver = new FakeDocumentSaver((_, path) => OperationResult.Success(path));
        var confirmation = new FakeSaveConfirmation(canSaveUnsupported: false);
        var viewModel = new MainViewModel(
            picker,
            loader,
            new LayoutEngine(),
            saver,
            confirmation);

        await viewModel.OpenDocumentCommand.ExecuteAsync(null);
        AddEdit(viewModel);

        await viewModel.SaveDocumentCommand.ExecuteAsync(null);

        confirmation.UnsupportedPromptCount.ShouldBe(1);
        saver.SaveCount.ShouldBe(0);
        viewModel.FilePath.ShouldBe(@"C:\docs\source.docx");
        viewModel.IsModified.ShouldBeTrue();
        viewModel.HasDiagnostics.ShouldBeTrue();
        viewModel.StatusText.ShouldContain("not acknowledged");
    }

    [Fact]
    public async Task Acknowledged_unsupported_content_save_writes_and_clears_notice()
    {
        var picker = new FakeFilePicker(@"C:\docs\source.docx");
        var loader = new FakeDocumentLoader((_, _) =>
            OperationResult.Success(
                CreateDocument("Opened"),
                [new Diagnostic(
                    DocxDiagnosticCodes.UnsupportedTable,
                    DiagnosticSeverity.Warning,
                    "Tables are not shown.")]));
        var saver = new FakeDocumentSaver((_, path) => OperationResult.Success(path));
        var confirmation = new FakeSaveConfirmation(canSaveUnsupported: true);
        var viewModel = new MainViewModel(
            picker,
            loader,
            new LayoutEngine(),
            saver,
            confirmation);

        await viewModel.OpenDocumentCommand.ExecuteAsync(null);
        AddEdit(viewModel);

        await viewModel.SaveDocumentCommand.ExecuteAsync(null);

        confirmation.UnsupportedPromptCount.ShouldBe(1);
        saver.SaveCount.ShouldBe(1);
        viewModel.IsModified.ShouldBeFalse();
        viewModel.HasDiagnostics.ShouldBeFalse();
        viewModel.StatusText.ShouldContain("acknowledged loss");
    }

    [Fact]
    public async Task Save_as_without_extension_adds_docx_after_confirmation()
    {
        var picker = new FakeFilePicker(openPath: null, savePath: @"C:\docs\new");
        var saver = new FakeDocumentSaver((_, path) => OperationResult.Success(path));
        var confirmation = new FakeSaveConfirmation(canAddExtension: true);
        var viewModel = new MainViewModel(
            picker,
            new FakeDocumentLoader((_, _) =>
                OperationResult.Failure<Document>("TEST", "Not used.")),
            new LayoutEngine(),
            saver,
            confirmation);

        AddEdit(viewModel);

        await viewModel.SaveDocumentAsCommand.ExecuteAsync(null);

        confirmation.ExtensionPromptCount.ShouldBe(1);
        saver.LastPath.ShouldBe(@"C:\docs\new.docx");
        viewModel.FilePath.ShouldBe(@"C:\docs\new.docx");
        viewModel.DocumentTitle.ShouldBe("new");
    }

    [Fact]
    public async Task Declining_docx_extension_keeps_document_unsaved()
    {
        var picker = new FakeFilePicker(openPath: null, savePath: @"C:\docs\new");
        var saver = new FakeDocumentSaver((_, path) => OperationResult.Success(path));
        var confirmation = new FakeSaveConfirmation(canAddExtension: false);
        var viewModel = new MainViewModel(
            picker,
            new FakeDocumentLoader((_, _) =>
                OperationResult.Failure<Document>("TEST", "Not used.")),
            new LayoutEngine(),
            saver,
            confirmation);

        AddEdit(viewModel);

        await viewModel.SaveDocumentAsCommand.ExecuteAsync(null);

        confirmation.ExtensionPromptCount.ShouldBe(1);
        saver.SaveCount.ShouldBe(0);
        viewModel.FilePath.ShouldBeEmpty();
        viewModel.IsModified.ShouldBeTrue();
        viewModel.StatusText.ShouldBe("Save canceled.");
    }

    [Fact]
    public async Task Save_as_with_non_docx_extension_reports_error_without_writing()
    {
        var picker = new FakeFilePicker(openPath: null, savePath: @"C:\docs\new.txt");
        var saver = new FakeDocumentSaver((_, path) => OperationResult.Success(path));
        var viewModel = new MainViewModel(
            picker,
            new FakeDocumentLoader((_, _) =>
                OperationResult.Failure<Document>("TEST", "Not used.")),
            new LayoutEngine(),
            saver,
            new FakeSaveConfirmation());

        AddEdit(viewModel);

        await viewModel.SaveDocumentAsCommand.ExecuteAsync(null);

        saver.SaveCount.ShouldBe(0);
        viewModel.FilePath.ShouldBeEmpty();
        viewModel.IsModified.ShouldBeTrue();
        viewModel.ErrorMessage.ShouldContain("Only .docx");
    }

    private static MainViewModel CreateViewModel() =>
        new(
            new FakeFilePicker(null),
            new FakeDocumentLoader((_, _) =>
                OperationResult.Failure<Document>("TEST", "Not used.")),
            new LayoutEngine());

    private static void AddEdit(MainViewModel viewModel)
    {
        var paragraph = viewModel.History.Current.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .First();

        viewModel.ApplyEdit(new InsertTextCommand(paragraph.Id, Run.Create(" edited")));
    }

    private static Document CreateDocument(string title)
    {
        var paragraph = ParagraphBlock.Create("Hello");
        var section = new Section(Guid.NewGuid(), ImmutableArray.Create<Block>(paragraph));
        return new Document(Guid.NewGuid(), title, ImmutableArray.Create(section));
    }

    private sealed class FakeFilePicker(string? openPath, string? savePath = null) : IDocxFilePicker
    {
        public int SavePickerCalls { get; private set; }

        public string? LastSuggestedSaveName { get; private set; }

        public Task<string?> PickOpenFileAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(openPath);

        public Task<string?> PickSaveFileAsync(
            string suggestedFileName,
            CancellationToken cancellationToken = default)
        {
            SavePickerCalls++;
            LastSuggestedSaveName = suggestedFileName;
            return Task.FromResult(savePath);
        }
    }

    private sealed class FakeDocumentLoader(
        Func<string, CancellationToken, OperationResult<Document>> handler) : IDocxDocumentLoader
    {
        public string? LastPath { get; private set; }

        public OperationResult<Document> Load(
            string path,
            CancellationToken cancellationToken = default)
        {
            LastPath = path;
            return handler(path, cancellationToken);
        }
    }

    private sealed class FakeDocumentSaver(
        Func<Document, string, OperationResult<string>> handler) : IDocxDocumentSaver
    {
        public int SaveCount { get; private set; }

        public string? LastPath { get; private set; }

        public OperationResult<string> Save(
            Document document,
            string path,
            CancellationToken cancellationToken = default)
        {
            SaveCount++;
            LastPath = path;
            return handler(document, path);
        }
    }

    private sealed class FakeSaveConfirmation(
        bool canSaveUnsupported = false,
        bool canAddExtension = false) : ISaveConfirmation
    {
        public int UnsupportedPromptCount { get; private set; }

        public int ExtensionPromptCount { get; private set; }

        public Task<bool> ConfirmSaveDespiteUnsupportedContentAsync(
            IReadOnlyList<Diagnostic> diagnostics,
            CancellationToken cancellationToken = default)
        {
            UnsupportedPromptCount++;
            return Task.FromResult(canSaveUnsupported);
        }

        public Task<bool> ConfirmAddDocxExtensionAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            ExtensionPromptCount++;
            return Task.FromResult(canAddExtension);
        }
    }

    private sealed class CancellableDocumentSaver : IDocxDocumentSaver
    {
        public ManualResetEventSlim Started { get; } = new();

        public OperationResult<string> Save(
            Document document,
            string path,
            CancellationToken cancellationToken = default)
        {
            Started.Set();
            cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
            cancellationToken.ThrowIfCancellationRequested();
            return OperationResult.Success(path);
        }
    }
}
