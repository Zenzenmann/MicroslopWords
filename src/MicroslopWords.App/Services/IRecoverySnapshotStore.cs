using System.Collections.Immutable;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;

namespace MicroslopWords.App.Services;

public sealed record RecoverySnapshot(
    Guid DocumentId,
    string Title,
    string SourcePath,
    DateTimeOffset Timestamp,
    string SnapshotPath,
    bool IsReadable,
    string ErrorMessage);

public sealed record RecoveredDocument(
    Document Document,
    string SourcePath,
    DateTimeOffset Timestamp);

public interface IRecoverySnapshotStore
{
    OperationResult<RecoverySnapshot> Save(
        Document document,
        string? sourcePath,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default);

    ImmutableArray<RecoverySnapshot> ListSnapshots();

    OperationResult<RecoveredDocument> Load(
        RecoverySnapshot snapshot,
        CancellationToken cancellationToken = default);

    OperationResult<bool> Discard(
        RecoverySnapshot snapshot,
        CancellationToken cancellationToken = default);

    OperationResult<bool> DeleteForDocument(
        Guid documentId,
        CancellationToken cancellationToken = default);
}
