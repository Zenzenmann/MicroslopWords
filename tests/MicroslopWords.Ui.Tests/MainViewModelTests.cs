using System.Collections.Immutable;
using MicroslopWords.App.Services;
using MicroslopWords.App.ViewModels;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
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

    private static MainViewModel CreateViewModel() =>
        new(new FakeFilePicker(null), new FakeDocumentLoader((_, _) =>
            OperationResult.Failure<Document>("TEST", "Not used.")), new LayoutEngine());

    private static Document CreateDocument(string title)
    {
        var paragraph = ParagraphBlock.Create("Hello");
        var section = new Section(Guid.NewGuid(), ImmutableArray.Create<Block>(paragraph));
        return new Document(Guid.NewGuid(), title, ImmutableArray.Create(section));
    }

    private sealed class FakeFilePicker(string? path) : IDocxFilePicker
    {
        public Task<string?> PickOpenFileAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(path);
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
}
