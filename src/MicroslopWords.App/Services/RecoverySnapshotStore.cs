using System.Collections.Immutable;
using System.IO.Compression;
using System.Text.Json;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using MicroslopWords.Formats.Docx;

namespace MicroslopWords.App.Services;

public sealed class RecoverySnapshotStore : IRecoverySnapshotStore
{
    public const string FileExtension = ".msw-recovery";

    private const string ManifestEntryName = "manifest.json";
    private const string DocumentEntryName = "document.docx";
    private const int FormatVersion = 1;
    private const int MaximumSnapshots = 20;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private readonly string _rootPath;
    private readonly IAtomicFileWriter _fileWriter;

    public RecoverySnapshotStore()
        : this(GetDefaultRootPath(), new AtomicFileWriter())
    {
    }

    public RecoverySnapshotStore(string rootPath)
        : this(rootPath, new AtomicFileWriter())
    {
    }

    internal RecoverySnapshotStore(string rootPath, IAtomicFileWriter fileWriter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(fileWriter);

        _rootPath = Path.GetFullPath(rootPath);
        _fileWriter = fileWriter;
    }

    public OperationResult<RecoverySnapshot> Save(
        Document document,
        string? sourcePath,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(_rootPath);

            var path = GetSnapshotPath(document.Id);
            var manifest = new SnapshotManifest
            {
                FormatVersion = FormatVersion,
                DocumentId = document.Id,
                Title = document.Title,
                SourcePath = sourcePath ?? string.Empty,
                Timestamp = timestamp.ToUniversalTime(),
            };

            _fileWriter.Write(
                path,
                stream =>
                {
                    using var archive = new ZipArchive(
                        stream,
                        ZipArchiveMode.Create,
                        leaveOpen: true);

                    var manifestEntry = archive.CreateEntry(
                        ManifestEntryName,
                        CompressionLevel.Optimal);

                    using (var manifestStream = manifestEntry.Open())
                    {
                        JsonSerializer.Serialize(manifestStream, manifest, JsonOptions);
                    }

                    var documentEntry = archive.CreateEntry(
                        DocumentEntryName,
                        CompressionLevel.Optimal);

                    using (var documentStream = documentEntry.Open())
                    {
                        using var docxStream = new MemoryStream();
                        DocxService.Save(document, docxStream);
                        documentStream.Write(docxStream.ToArray());
                    }
                },
                cancellationToken);

            var snapshot = new RecoverySnapshot(
                manifest.DocumentId,
                manifest.Title,
                manifest.SourcePath,
                manifest.Timestamp,
                path,
                IsReadable: true,
                ErrorMessage: string.Empty);

            PruneOldSnapshots(document.Id);
            return OperationResult.Success(snapshot);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(
                RecoveryDiagnosticCodes.SnapshotWriteFailed,
                "A recovery snapshot cannot be written. Check your local app data permissions.");
        }
        catch (System.Security.SecurityException)
        {
            return Failure(
                RecoveryDiagnosticCodes.SnapshotWriteFailed,
                "A recovery snapshot cannot be written. Check your local app data permissions.");
        }
        catch (Exception exception) when (
            exception is IOException
                or InvalidDataException
                or NotSupportedException
                or ArgumentException)
        {
            return Failure(
                RecoveryDiagnosticCodes.SnapshotWriteFailed,
                "A recovery snapshot could not be written. Your document is still open.");
        }
    }

    public ImmutableArray<RecoverySnapshot> ListSnapshots()
    {
        try
        {
            if (!Directory.Exists(_rootPath))
            {
                return ImmutableArray<RecoverySnapshot>.Empty;
            }

            var snapshots = ImmutableArray.CreateBuilder<RecoverySnapshot>();

            foreach (var path in Directory.EnumerateFiles(
                         _rootPath,
                         $"*{FileExtension}",
                         SearchOption.TopDirectoryOnly))
            {
                snapshots.Add(ReadSnapshot(path));
            }

            return snapshots
                .OrderByDescending(snapshot => snapshot.Timestamp)
                .ToImmutableArray();
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or System.Security.SecurityException
                or NotSupportedException
                or ArgumentException)
        {
            return ImmutableArray<RecoverySnapshot>.Empty;
        }
    }

    public OperationResult<RecoveredDocument> Load(
        RecoverySnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(snapshot.SnapshotPath))
            {
                return OperationResult.Failure<RecoveredDocument>(
                    RecoveryDiagnosticCodes.SnapshotMissing,
                    "The recovery snapshot no longer exists.");
            }

            using var fileStream = new FileStream(
                snapshot.SnapshotPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);

            var manifest = ReadManifest(archive);
            var documentEntry = archive.GetEntry(DocumentEntryName)
                ?? throw new InvalidDataException("The recovery snapshot has no document.");

            using var documentStream = new MemoryStream();
            using (var entryStream = documentEntry.Open())
            {
                entryStream.CopyTo(documentStream);
            }

            documentStream.Position = 0;
            var title = string.IsNullOrWhiteSpace(manifest.Title)
                ? "Recovered draft"
                : manifest.Title;
            var result = DocxService.Load(
                documentStream,
                title,
                cancellationToken);

            if (!result.IsSuccess || result.Value is null)
            {
                return OperationResult.Failure<RecoveredDocument>(
                    result.Diagnostics.ToArray());
            }

            var document = result.Value with
            {
                Id = manifest.DocumentId == Guid.Empty
                    ? result.Value.Id
                    : manifest.DocumentId,
                Title = title,
            };

            return OperationResult.Success(
                new RecoveredDocument(
                    document,
                    manifest.SourcePath,
                    manifest.Timestamp),
                result.Diagnostics);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException
                or InvalidDataException
                or JsonException
                or UnauthorizedAccessException
                or System.Security.SecurityException
                or NotSupportedException
                or ArgumentException)
        {
            return FailureRecovered(
                RecoveryDiagnosticCodes.SnapshotUnreadable,
                "The recovery snapshot cannot be read. You can discard it.");
        }
    }

    public OperationResult<bool> Discard(
        RecoverySnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = GetValidatedSnapshotPath(snapshot.SnapshotPath);
            File.Delete(path);
            return OperationResult.Success(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or System.Security.SecurityException
                or NotSupportedException
                or ArgumentException)
        {
            return FailureBool(
                RecoveryDiagnosticCodes.SnapshotDiscardFailed,
                "The recovery snapshot could not be discarded.");
        }
    }

    public OperationResult<bool> DeleteForDocument(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = GetSnapshotPath(documentId);
            File.Delete(path);
            return OperationResult.Success(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or System.Security.SecurityException
                or NotSupportedException
                or ArgumentException)
        {
            return FailureBool(
                RecoveryDiagnosticCodes.SnapshotDiscardFailed,
                "The recovery snapshot could not be removed after saving.");
        }
    }

    private static string GetDefaultRootPath()
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.Combine(
                Path.GetTempPath(),
                Environment.UserName ?? "MicroslopWords");
        }

        return Path.Combine(localAppData, "MicroslopWords", "Recovery");
    }

    private string GetSnapshotPath(Guid documentId) =>
        Path.Combine(_rootPath, $"{documentId:N}{FileExtension}");

    private string GetValidatedSnapshotPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.Equals(directory, _rootPath, PathComparison)
            || !string.Equals(
                Path.GetExtension(fullPath),
                FileExtension,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The snapshot is outside the recovery store.", nameof(path));
        }

        return fullPath;
    }

    private static RecoverySnapshot ReadSnapshot(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        var documentId = Guid.TryParse(fileName, out var parsedId)
            ? parsedId
            : Guid.Empty;
        var fallbackTimestamp = new DateTimeOffset(
            File.GetLastWriteTimeUtc(path),
            TimeSpan.Zero);

        try
        {
            using var fileStream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
            var manifest = ReadManifest(archive);

            if (archive.GetEntry(DocumentEntryName) is null)
            {
                throw new InvalidDataException("The recovery snapshot has no document.");
            }

            return new RecoverySnapshot(
                manifest.DocumentId == Guid.Empty
                    ? documentId
                    : manifest.DocumentId,
                string.IsNullOrWhiteSpace(manifest.Title)
                    ? "Recovered draft"
                    : manifest.Title,
                manifest.SourcePath,
                manifest.Timestamp == default
                    ? fallbackTimestamp
                    : manifest.Timestamp,
                path,
                IsReadable: true,
                ErrorMessage: string.Empty);
        }
        catch (Exception exception) when (
            exception is IOException
                or InvalidDataException
                or JsonException
                or UnauthorizedAccessException
                or System.Security.SecurityException
                or NotSupportedException
                or ArgumentException)
        {
            return new RecoverySnapshot(
                documentId,
                "Recovery snapshot",
                string.Empty,
                fallbackTimestamp,
                path,
                IsReadable: false,
                "This recovery snapshot cannot be read. You can discard it.");
        }
    }

    private static SnapshotManifest ReadManifest(ZipArchive archive)
    {
        var manifestEntry = archive.GetEntry(ManifestEntryName)
            ?? throw new InvalidDataException("The recovery snapshot has no manifest.");

        using var manifestStream = manifestEntry.Open();
        var manifest = JsonSerializer.Deserialize<SnapshotManifest>(
            manifestStream,
            JsonOptions);

        return manifest
            ?? throw new InvalidDataException("The recovery snapshot manifest is invalid.");
    }

    private void PruneOldSnapshots(Guid currentDocumentId)
    {
        try
        {
            var staleSnapshots = ListSnapshots()
                .Where(snapshot => snapshot.DocumentId != currentDocumentId)
                .Skip(MaximumSnapshots - 1)
                .ToArray();

            foreach (var snapshot in staleSnapshots)
            {
                File.Delete(snapshot.SnapshotPath);
            }
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or System.Security.SecurityException
                or NotSupportedException
                or ArgumentException)
        {
        }
    }

    private static OperationResult<RecoverySnapshot> Failure(string code, string message) =>
        OperationResult.Failure<RecoverySnapshot>(
            new Diagnostic(code, DiagnosticSeverity.Error, message));

    private static OperationResult<RecoveredDocument> FailureRecovered(
        string code,
        string message) =>
        OperationResult.Failure<RecoveredDocument>(
            new Diagnostic(code, DiagnosticSeverity.Error, message));

    private static OperationResult<bool> FailureBool(string code, string message) =>
        OperationResult.Failure<bool>(
            new Diagnostic(code, DiagnosticSeverity.Error, message));

    private sealed class SnapshotManifest
    {
        public int FormatVersion { get; set; }

        public Guid DocumentId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string SourcePath { get; set; } = string.Empty;

        public DateTimeOffset Timestamp { get; set; }
    }
}
