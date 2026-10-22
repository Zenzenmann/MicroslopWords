using System.Collections.Immutable;
using MicroslopWords.App.Services;
using MicroslopWords.App.ViewModels;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using MicroslopWords.Layout;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class MainViewModelRecoveryTests
{
    [Fact]
    public async Task Capture_writes_snapshot_only_while_document_is_dirty()
    {
        var store = new FakeRecoveryStore();
        var viewModel = CreateViewModel(store, new FakeRecoveryPrompt());

        await viewModel.CaptureRecoverySnapshotAsync(
            TestContext.Current.CancellationToken);
        store.SaveCount.ShouldBe(0);

        AddEdit(viewModel);
        await viewModel.CaptureRecoverySnapshotAsync(
            TestContext.Current.CancellationToken);
        store.SaveCount.ShouldBe(1);

        await viewModel.SaveDocumentCommand.ExecuteAsync(null);
        await viewModel.CaptureRecoverySnapshotAsync(
            TestContext.Current.CancellationToken);

        store.SaveCount.ShouldBe(1);
        store.DeleteCount.ShouldBe(1);
    }

    [Fact]
    public async Task Autosave_interval_writes_snapshot_for_dirty_document()
    {
        var store = new FakeRecoveryStore();
        var viewModel = CreateViewModel(
            store,
            new FakeRecoveryPrompt(),
            TimeSpan.FromMilliseconds(25));

        try
        {
            AddEdit(viewModel);

            await WaitUntilAsync(
                () => store.SaveCount > 0,
                TimeSpan.FromSeconds(5));
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task Snapshot_uses_current_time_for_its_timestamp()
    {
        var timestamp = new DateTimeOffset(2026, 10, 22, 12, 30, 0, TimeSpan.Zero);
        DateTimeOffset? captured = null;
        var store = new FakeRecoveryStore
        {
            SaveHandler = (document, sourcePath, savedAt) =>
            {
                captured = savedAt;
                return OperationResult.Success(
                    new RecoverySnapshot(
                        document.Id,
                        document.Title,
                        sourcePath ?? string.Empty,
                        savedAt,
                        TestPath($"{document.Id:N}.msw-recovery"),
                        IsReadable: true,
                        ErrorMessage: string.Empty));
            },
        };
        var viewModel = CreateViewModel(
            store,
            new FakeRecoveryPrompt(),
            timeProvider: new FixedTimeProvider(timestamp));

        AddEdit(viewModel);
        await viewModel.CaptureRecoverySnapshotAsync(
            TestContext.Current.CancellationToken);

        captured.ShouldBe(timestamp);
        viewModel.Dispose();
    }

    [Fact]
    public void Public_autosave_interval_is_limited_to_30_through_300_seconds()
    {
        var store = new FakeRecoveryStore();
        var prompt = new FakeRecoveryPrompt();
        var picker = new FakeFilePicker(savePath: null);
        var loader = new FakeDocumentLoader((_, _) =>
            OperationResult.Failure<Document>("TEST", "Not used."));

        Should.Throw<ArgumentOutOfRangeException>(() =>
            new MainViewModel(
                picker,
                loader,
                new LayoutEngine(),
                new FakeDocumentSaver(),
                new FakeSaveConfirmation(),
                store,
                prompt,
                TimeSpan.FromSeconds(29)));

        Should.Throw<ArgumentOutOfRangeException>(() =>
            new MainViewModel(
                picker,
                loader,
                new LayoutEngine(),
                new FakeDocumentSaver(),
                new FakeSaveConfirmation(),
                store,
                prompt,
                TimeSpan.FromSeconds(301)));

        using var minimum = new MainViewModel(
            picker,
            loader,
            new LayoutEngine(),
            new FakeDocumentSaver(),
            new FakeSaveConfirmation(),
            store,
            prompt,
            TimeSpan.FromSeconds(30));
        using var maximum = new MainViewModel(
            picker,
            loader,
            new LayoutEngine(),
            new FakeDocumentSaver(),
            new FakeSaveConfirmation(),
            store,
            prompt,
            TimeSpan.FromSeconds(300));
    }

    [Fact]
    public async Task Snapshot_failure_warns_once_and_keeps_document_dirty()
    {
        var store = new FakeRecoveryStore
        {
            SaveHandler = (_, _, _) => OperationResult.Failure<RecoverySnapshot>(
                RecoveryDiagnosticCodes.SnapshotWriteFailed,
                "A recovery snapshot could not be written."),
        };
        var viewModel = CreateViewModel(store, new FakeRecoveryPrompt());
        var errorChanges = 0;

        AddEdit(viewModel);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.ErrorMessage))
            {
                errorChanges++;
            }
        };

        await viewModel.CaptureRecoverySnapshotAsync(
            TestContext.Current.CancellationToken);
        await viewModel.CaptureRecoverySnapshotAsync(
            TestContext.Current.CancellationToken);

        store.SaveCount.ShouldBe(2);
        errorChanges.ShouldBe(1);
        viewModel.IsModified.ShouldBeTrue();
        viewModel.ErrorMessage.ShouldContain("still open");
    }

    [Fact]
    public async Task Successful_save_deletes_recovery_snapshot()
    {
        var store = new FakeRecoveryStore();
        var viewModel = CreateViewModel(store, new FakeRecoveryPrompt());
        AddEdit(viewModel);

        await viewModel.CaptureRecoverySnapshotAsync(
            TestContext.Current.CancellationToken);
        await viewModel.SaveDocumentCommand.ExecuteAsync(null);

        store.SaveCount.ShouldBe(1);
        store.DeleteCount.ShouldBe(1);
        store.LastDeletedDocumentId.ShouldBe(viewModel.History.Current.Id);
        viewModel.IsModified.ShouldBeFalse();
    }

    [Fact]
    public async Task Unsupported_document_disables_recovery_and_reports_once()
    {
        var store = new FakeRecoveryStore();
        var loader = new FakeDocumentLoader((_, _) =>
            OperationResult.Success(
                CreateDocument("Opened", "Hello"),
                [new Diagnostic(
                    "MSW2001",
                    DiagnosticSeverity.Warning,
                    "Tables are not shown.")]));
        var viewModel = CreateViewModel(
            store,
            new FakeRecoveryPrompt(),
            picker: new FakeFilePicker(
                openPath: TestPath("opened.docx"),
                savePath: TestPath("saved.docx")),
            loader: loader);

        await viewModel.OpenDocumentCommand.ExecuteAsync(null);
        AddEdit(viewModel);

        var diagnosticChanges = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.DiagnosticsSummary))
            {
                diagnosticChanges++;
            }
        };

        await viewModel.CaptureRecoverySnapshotAsync(
            TestContext.Current.CancellationToken);
        await viewModel.CaptureRecoverySnapshotAsync(
            TestContext.Current.CancellationToken);

        store.SaveCount.ShouldBe(0);
        diagnosticChanges.ShouldBe(1);
        viewModel.DiagnosticsSummary.ShouldContain("Crash recovery is unavailable");
        viewModel.IsModified.ShouldBeTrue();
    }

    [Fact]
    public async Task Recovery_prompt_opens_dirty_draft_without_touching_source()
    {
        var directory = CreateTemporaryDirectory();
        var sourcePath = Path.Combine(directory, "source.docx");

        try
        {
            File.WriteAllText(sourcePath, "original");
            var store = new RecoverySnapshotStore(Path.Combine(directory, "recovery"));
            var document = CreateDocument("Recovered", "Recovered text");
            store.Save(
                document,
                sourcePath,
                DateTimeOffset.UtcNow,
                TestContext.Current.CancellationToken);
            var prompt = new FakeRecoveryPrompt
            {
                Choice = RecoveryPromptChoice.OpenRecoveredDraft,
            };
            var viewModel = CreateViewModel(store, prompt);

            await viewModel.InitializeRecoveryAsync(
                TestContext.Current.CancellationToken);

            viewModel.History.Current.Id.ShouldBe(document.Id);
            viewModel.History.Current.PlainText.ShouldBe("Recovered text");
            viewModel.FilePath.ShouldBeEmpty();
            viewModel.IsModified.ShouldBeTrue();
            viewModel.WindowTitle.ShouldBe("Recovered* - MicroslopWords");
            viewModel.StatusText.ShouldContain("Recovered unsaved draft");
            File.ReadAllText(sourcePath).ShouldBe("original");
            store.ListSnapshots().Length.ShouldBe(1);

            var savedPath = Path.Combine(directory, "saved.docx");
            var picker = new FakeFilePicker(savePath: savedPath);
            var savedViewModel = CreateViewModel(
                store,
                prompt,
                picker: picker,
                saver: new FakeDocumentSaver());

            await savedViewModel.InitializeRecoveryAsync(
                TestContext.Current.CancellationToken);
            await savedViewModel.SaveDocumentCommand.ExecuteAsync(null);

            store.ListSnapshots().ShouldBeEmpty();
            File.ReadAllText(sourcePath).ShouldBe("original");
            viewModel.Dispose();
            savedViewModel.Dispose();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Recovery_discard_requires_confirmation()
    {
        var store = new FakeRecoveryStore
        {
            Snapshots =
            [
                CreateSnapshot(Guid.NewGuid(), isReadable: true),
            ],
        };
        var prompt = new FakeRecoveryPrompt
        {
            Choice = RecoveryPromptChoice.Discard,
            ConfirmDiscard = false,
        };
        var viewModel = CreateViewModel(store, prompt);

        await viewModel.InitializeRecoveryAsync(
            TestContext.Current.CancellationToken);

        prompt.ConfirmDiscardCount.ShouldBe(1);
        store.DiscardCount.ShouldBe(0);

        prompt.ConfirmDiscard = true;
        await viewModel.InitializeRecoveryAsync(
            TestContext.Current.CancellationToken);

        store.DiscardCount.ShouldBe(1);
    }

    [Fact]
    public async Task Corrupt_recovery_snapshot_is_prompted_and_can_be_discarded()
    {
        var store = new FakeRecoveryStore
        {
            Snapshots =
            [
                CreateSnapshot(Guid.NewGuid(), isReadable: false),
            ],
        };
        var prompt = new FakeRecoveryPrompt
        {
            Choice = RecoveryPromptChoice.Discard,
            ConfirmDiscard = true,
        };
        var viewModel = CreateViewModel(store, prompt);

        await viewModel.InitializeRecoveryAsync(
            TestContext.Current.CancellationToken);

        prompt.LastSnapshot.ShouldNotBeNull();
        prompt.LastSnapshot.IsReadable.ShouldBeFalse();
        prompt.LastSnapshot.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
        store.DiscardCount.ShouldBe(1);
    }

    [Fact]
    public async Task Recovery_load_failure_reports_error_and_keeps_snapshot()
    {
        var store = new FakeRecoveryStore
        {
            Snapshots =
            [
                CreateSnapshot(Guid.NewGuid(), isReadable: true),
            ],
            LoadHandler = (_, _) => OperationResult.Failure<RecoveredDocument>(
                RecoveryDiagnosticCodes.SnapshotUnreadable,
                "The recovery snapshot cannot be read."),
        };
        var prompt = new FakeRecoveryPrompt
        {
            Choice = RecoveryPromptChoice.OpenRecoveredDraft,
        };
        var viewModel = CreateViewModel(store, prompt);

        await viewModel.InitializeRecoveryAsync(
            TestContext.Current.CancellationToken);

        viewModel.HasError.ShouldBeTrue();
        viewModel.ErrorMessage.ShouldContain("cannot be read");
        store.DiscardCount.ShouldBe(0);
        store.ListSnapshots().Length.ShouldBe(1);
    }

    private static MainViewModel CreateViewModel(
        IRecoverySnapshotStore store,
        IRecoveryPrompt prompt,
        TimeSpan? autosaveInterval = null,
        FakeFilePicker? picker = null,
        FakeDocumentLoader? loader = null,
        FakeDocumentSaver? saver = null,
        TimeProvider? timeProvider = null) =>
        new(
            picker ?? new FakeFilePicker(savePath: TestPath("saved.docx")),
            loader ?? new FakeDocumentLoader((_, _) =>
                OperationResult.Failure<Document>("TEST", "Not used.")),
            new LayoutEngine(),
            saver ?? new FakeDocumentSaver(),
            new FakeSaveConfirmation(),
            store,
            prompt,
            autosaveInterval ?? TimeSpan.FromSeconds(60),
            timeProvider ?? TimeProvider.System);

    private static void AddEdit(MainViewModel viewModel)
    {
        var paragraph = viewModel.History.Current.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .First();

        viewModel.ApplyEdit(new InsertTextCommand(paragraph.Id, Run.Create(" edited")));
    }

    private static Document CreateDocument(string title, string text)
    {
        var paragraph = ParagraphBlock.Create(text);
        var section = new Section(Guid.NewGuid(), ImmutableArray.Create<Block>(paragraph));
        return new Document(Guid.NewGuid(), title, ImmutableArray.Create(section));
    }

    private static RecoverySnapshot CreateSnapshot(Guid documentId, bool isReadable) =>
        new(
            documentId,
            "Recovered draft",
            string.Empty,
            DateTimeOffset.UtcNow,
            TestPath($"{documentId:N}.msw-recovery"),
            isReadable,
            isReadable ? string.Empty : "This recovery snapshot cannot be read.");

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        condition().ShouldBeTrue();
    }

    private static string TestPath(string fileName) =>
        Path.Combine(Path.GetTempPath(), "MicroslopWords.Tests", fileName);

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "MicroslopWords.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class FakeRecoveryStore : IRecoverySnapshotStore
    {
        public ImmutableArray<RecoverySnapshot> Snapshots { get; set; } =
            ImmutableArray<RecoverySnapshot>.Empty;

        public Func<Document, string?, DateTimeOffset, OperationResult<RecoverySnapshot>>?
            SaveHandler
        { get; set; }

        public Func<RecoverySnapshot, CancellationToken, OperationResult<RecoveredDocument>>?
            LoadHandler
        { get; set; }

        public int SaveCount { get; private set; }

        public int DiscardCount { get; private set; }

        public int DeleteCount { get; private set; }

        public Guid? LastDeletedDocumentId { get; private set; }

        public OperationResult<RecoverySnapshot> Save(
            Document document,
            string? sourcePath,
            DateTimeOffset timestamp,
            CancellationToken cancellationToken = default)
        {
            SaveCount++;

            if (SaveHandler is not null)
            {
                return SaveHandler(document, sourcePath, timestamp);
            }

            var snapshot = new RecoverySnapshot(
                document.Id,
                document.Title,
                sourcePath ?? string.Empty,
                timestamp,
                TestPath($"{document.Id:N}.msw-recovery"),
                IsReadable: true,
                ErrorMessage: string.Empty);
            Snapshots = [snapshot];
            return OperationResult.Success(snapshot);
        }

        public ImmutableArray<RecoverySnapshot> ListSnapshots() => Snapshots;

        public OperationResult<RecoveredDocument> Load(
            RecoverySnapshot snapshot,
            CancellationToken cancellationToken = default) =>
            LoadHandler?.Invoke(snapshot, cancellationToken)
            ?? OperationResult.Failure<RecoveredDocument>(
                RecoveryDiagnosticCodes.SnapshotUnreadable,
                "The recovery snapshot cannot be read.");

        public OperationResult<bool> Discard(
            RecoverySnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            DiscardCount++;
            Snapshots = Snapshots.Remove(snapshot);
            return OperationResult.Success(true);
        }

        public OperationResult<bool> DeleteForDocument(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            DeleteCount++;
            LastDeletedDocumentId = documentId;
            Snapshots = Snapshots
                .Where(snapshot => snapshot.DocumentId != documentId)
                .ToImmutableArray();
            return OperationResult.Success(true);
        }
    }

    private sealed class FakeRecoveryPrompt : IRecoveryPrompt
    {
        public RecoveryPromptChoice Choice { get; set; } = RecoveryPromptChoice.Later;

        public bool ConfirmDiscard { get; set; }

        public int PromptCount { get; private set; }

        public int ConfirmDiscardCount { get; private set; }

        public RecoverySnapshot? LastSnapshot { get; private set; }

        public Task<RecoveryPromptChoice> PromptAsync(
            RecoverySnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            PromptCount++;
            LastSnapshot = snapshot;
            return Task.FromResult(Choice);
        }

        public Task<bool> ConfirmDiscardAsync(
            RecoverySnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            ConfirmDiscardCount++;
            return Task.FromResult(ConfirmDiscard);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeFilePicker(string? openPath, string? savePath) : IDocxFilePicker
    {
        public FakeFilePicker(string? savePath)
            : this(openPath: null, savePath: savePath)
        {
        }

        public Task<string?> PickOpenFileAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(openPath);

        public Task<string?> PickSaveFileAsync(
            string suggestedFileName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(savePath);
    }

    private sealed class FakeDocumentLoader(
        Func<string, CancellationToken, OperationResult<Document>> handler) : IDocxDocumentLoader
    {
        public OperationResult<Document> Load(
            string path,
            CancellationToken cancellationToken = default) =>
            handler(path, cancellationToken);
    }

    private sealed class FakeDocumentSaver : IDocxDocumentSaver
    {
        public OperationResult<string> Save(
            Document document,
            string path,
            CancellationToken cancellationToken = default) =>
            OperationResult.Success(path);
    }

    private sealed class FakeSaveConfirmation : ISaveConfirmation
    {
        public Task<bool> ConfirmSaveDespiteUnsupportedContentAsync(
            IReadOnlyList<Diagnostic> diagnostics,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> ConfirmAddDocxExtensionAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
