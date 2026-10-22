using System.Collections.Immutable;
using MicroslopWords.App.Services;
using MicroslopWords.Core.Documents;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class RecoverySnapshotStoreTests
{
    [Fact]
    public void Save_writes_snapshot_that_loads_with_original_identity_and_source()
    {
        var directory = CreateTemporaryDirectory();
        var recoveryRoot = Path.Combine(directory, "recovery");
        var sourcePath = Path.Combine(directory, "source.docx");
        var timestamp = DateTimeOffset.UtcNow;

        try
        {
            File.WriteAllText(sourcePath, "original");
            var store = new RecoverySnapshotStore(recoveryRoot);
            var document = CreateDocument("Draft", "Hello recovery");

            var result = store.Save(
                document,
                sourcePath,
                timestamp,
                TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value.DocumentId.ShouldBe(document.Id);
            result.Value.IsReadable.ShouldBeTrue();
            File.Exists(result.Value.SnapshotPath).ShouldBeTrue();

            var snapshots = store.ListSnapshots();
            snapshots.Length.ShouldBe(1);
            snapshots[0].SourcePath.ShouldBe(sourcePath);

            var loaded = store.Load(snapshots[0], TestContext.Current.CancellationToken);

            loaded.IsSuccess.ShouldBeTrue();
            loaded.Value.ShouldNotBeNull();
            loaded.Value.Document.Id.ShouldBe(document.Id);
            loaded.Value.Document.Title.ShouldBe("Draft");
            loaded.Value.Document.PlainText.ShouldBe("Hello recovery");
            loaded.Value.SourcePath.ShouldBe(sourcePath);
            loaded.Value.Timestamp.ShouldBe(timestamp.ToUniversalTime());
            File.ReadAllText(sourcePath).ShouldBe("original");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Save_replaces_previous_snapshot_for_same_document()
    {
        var directory = CreateTemporaryDirectory();
        var recoveryRoot = Path.Combine(directory, "recovery");

        try
        {
            var store = new RecoverySnapshotStore(recoveryRoot);
            var document = CreateDocument("Draft", "First version");

            store.Save(
                document,
                sourcePath: null,
                DateTimeOffset.UtcNow,
                TestContext.Current.CancellationToken);

            var paragraph = document.Sections
                .SelectMany(section => section.Blocks)
                .OfType<ParagraphBlock>()
                .First();
            var updated = document.ReplaceBlock(
                paragraph with { Runs = ImmutableArray.Create(Run.Create("Second version")) });
            var result = store.Save(
                updated,
                sourcePath: null,
                DateTimeOffset.UtcNow.AddMinutes(1),
                TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue();
            store.ListSnapshots().Length.ShouldBe(1);
            Directory.GetFiles(recoveryRoot).Length.ShouldBe(1);

            var loaded = store.Load(
                store.ListSnapshots()[0],
                TestContext.Current.CancellationToken);

            loaded.Value.ShouldNotBeNull();
            loaded.Value.Document.PlainText.ShouldBe("Second version");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Save_replacement_failure_keeps_previous_snapshot()
    {
        var directory = CreateTemporaryDirectory();
        var recoveryRoot = Path.Combine(directory, "recovery");

        try
        {
            var normalStore = new RecoverySnapshotStore(recoveryRoot);
            var document = CreateDocument("Draft", "Stable version");
            normalStore.Save(
                document,
                sourcePath: null,
                DateTimeOffset.UtcNow,
                TestContext.Current.CancellationToken);

            var failingStore = new RecoverySnapshotStore(
                recoveryRoot,
                new AtomicFileWriter(
                    (_, _, _) => throw new IOException("Injected replacement failure.")));

            var paragraph = document.Sections
                .SelectMany(section => section.Blocks)
                .OfType<ParagraphBlock>()
                .First();
            var result = failingStore.Save(
                document.ReplaceBlock(
                    paragraph with { Runs = ImmutableArray.Create(Run.Create("New version")) }),
                sourcePath: null,
                DateTimeOffset.UtcNow.AddMinutes(1),
                TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            normalStore.ListSnapshots().Length.ShouldBe(1);

            var loaded = normalStore.Load(
                normalStore.ListSnapshots()[0],
                TestContext.Current.CancellationToken);

            loaded.IsSuccess.ShouldBeTrue();
            loaded.Value.ShouldNotBeNull();
            loaded.Value.Document.PlainText.ShouldBe("Stable version");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void List_reports_unreadable_snapshot_and_discard_removes_it()
    {
        var directory = CreateTemporaryDirectory();
        var recoveryRoot = Path.Combine(directory, "recovery");
        Directory.CreateDirectory(recoveryRoot);
        var path = Path.Combine(
            recoveryRoot,
            $"{Guid.NewGuid():N}{RecoverySnapshotStore.FileExtension}");

        try
        {
            File.WriteAllText(path, "not a recovery archive");
            var store = new RecoverySnapshotStore(recoveryRoot);

            var snapshots = store.ListSnapshots();

            snapshots.Length.ShouldBe(1);
            snapshots[0].IsReadable.ShouldBeFalse();
            snapshots[0].ErrorMessage.ShouldNotBeNullOrWhiteSpace();

            var discard = store.Discard(
                snapshots[0],
                TestContext.Current.CancellationToken);

            discard.IsSuccess.ShouldBeTrue();
            File.Exists(path).ShouldBeFalse();
            store.ListSnapshots().ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Discard_removes_snapshot_without_touching_source()
    {
        var directory = CreateTemporaryDirectory();
        var recoveryRoot = Path.Combine(directory, "recovery");
        var sourcePath = Path.Combine(directory, "source.docx");

        try
        {
            File.WriteAllText(sourcePath, "original");
            var store = new RecoverySnapshotStore(recoveryRoot);
            var document = CreateDocument("Draft", "Hello");
            store.Save(
                document,
                sourcePath,
                DateTimeOffset.UtcNow,
                TestContext.Current.CancellationToken);

            var snapshot = store.ListSnapshots().Single();
            var result = store.Discard(
                snapshot,
                TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue();
            store.ListSnapshots().ShouldBeEmpty();
            File.ReadAllText(sourcePath).ShouldBe("original");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_missing_snapshot_reports_missing_without_throwing()
    {
        var directory = CreateTemporaryDirectory();
        var snapshot = new RecoverySnapshot(
            Guid.NewGuid(),
            "Draft",
            string.Empty,
            DateTimeOffset.UtcNow,
            Path.Combine(directory, $"missing{RecoverySnapshotStore.FileExtension}"),
            IsReadable: true,
            ErrorMessage: string.Empty);

        try
        {
            var store = new RecoverySnapshotStore(Path.Combine(directory, "recovery"));
            var result = store.Load(snapshot, TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            result.Diagnostics.Single().Code.ShouldBe(
                RecoveryDiagnosticCodes.SnapshotMissing);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Save_failure_does_not_expose_source_path_or_document_text()
    {
        var directory = CreateTemporaryDirectory();
        var recoveryRoot = Path.Combine(directory, "recovery");
        var sourcePath = Path.Combine(directory, "private-source.docx");
        var secret = "confidential draft text";

        try
        {
            var store = new RecoverySnapshotStore(
                recoveryRoot,
                new AtomicFileWriter(
                    (_, _, _) => throw new IOException("Injected replacement failure.")));
            var document = CreateDocument("Private", secret);

            var result = store.Save(
                document,
                sourcePath,
                DateTimeOffset.UtcNow,
                TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            result.Diagnostics.ShouldAllBe(
                diagnostic => !diagnostic.Message.Contains(sourcePath, StringComparison.Ordinal)
                    && !diagnostic.Message.Contains(secret, StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DeleteForDocument_removes_snapshot()
    {
        var directory = CreateTemporaryDirectory();
        var recoveryRoot = Path.Combine(directory, "recovery");

        try
        {
            var store = new RecoverySnapshotStore(recoveryRoot);
            var document = CreateDocument("Draft", "Hello");
            store.Save(
                document,
                sourcePath: null,
                DateTimeOffset.UtcNow,
                TestContext.Current.CancellationToken);

            var result = store.DeleteForDocument(
                document.Id,
                TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue();
            store.ListSnapshots().ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Save_rotates_oldest_snapshots_after_retention_limit()
    {
        var directory = CreateTemporaryDirectory();
        var recoveryRoot = Path.Combine(directory, "recovery");

        try
        {
            var store = new RecoverySnapshotStore(recoveryRoot);
            var documents = Enumerable.Range(0, 21)
                .Select(index => CreateDocument($"Draft {index}", $"Text {index}"))
                .ToArray();

            foreach (var document in documents)
            {
                var result = store.Save(
                    document,
                    sourcePath: null,
                    DateTimeOffset.UtcNow.AddMinutes(Array.IndexOf(documents, document)),
                    TestContext.Current.CancellationToken);
                result.IsSuccess.ShouldBeTrue();
            }

            var snapshots = store.ListSnapshots();
            snapshots.Length.ShouldBe(20);
            snapshots.ShouldNotContain(
                snapshot => snapshot.DocumentId == documents[0].Id);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Document CreateDocument(string title, string text)
    {
        var paragraph = ParagraphBlock.Create(text);
        var section = new Section(Guid.NewGuid(), ImmutableArray.Create<Block>(paragraph));
        return new Document(Guid.NewGuid(), title, ImmutableArray.Create(section));
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "MicroslopWords.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
