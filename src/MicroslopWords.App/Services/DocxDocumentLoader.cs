using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using MicroslopWords.Formats.Docx;

namespace MicroslopWords.App.Services;

public sealed class DocxDocumentLoader : IDocxDocumentLoader
{
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] CompoundFileSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    public OperationResult<Document> Load(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Failure(DocxDiagnosticCodes.EmptyPath, "Choose a DOCX file to open.");
        }

        if (!File.Exists(path))
        {
            return Failure(DocxDiagnosticCodes.FileMissing, "The selected file no longer exists.");
        }

        if (!string.Equals(Path.GetExtension(path), ".docx", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(DocxDiagnosticCodes.UnsupportedExtension, "Only .docx files are supported.");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.SequentialScan);

            if (stream.Length == 0)
            {
                return Failure(DocxDiagnosticCodes.EmptyFile, "The selected file is empty.");
            }

            var signature = new byte[8];
            var bytesRead = stream.Read(signature, 0, signature.Length);

            if (bytesRead >= ZipSignature.Length
                && signature.AsSpan(0, ZipSignature.Length).SequenceEqual(ZipSignature))
            {
                stream.Position = 0;
                var title = Path.GetFileNameWithoutExtension(path);
                return DocxService.Load(stream, title, cancellationToken);
            }

            if (bytesRead >= CompoundFileSignature.Length
                && signature.AsSpan(0, CompoundFileSignature.Length).SequenceEqual(CompoundFileSignature))
            {
                return Failure(
                    DocxDiagnosticCodes.EncryptedOrLegacyDocument,
                    "Encrypted and legacy Word files are not supported.");
            }

            return Failure(
                DocxDiagnosticCodes.InvalidSignature,
                "The selected file is not a DOCX package.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(
                DocxDiagnosticCodes.FileUnreadable,
                "The selected file cannot be read. Check its permissions and try again.");
        }
        catch (IOException)
        {
            return Failure(
                DocxDiagnosticCodes.FileUnreadable,
                "The selected file is locked or cannot be read. Close it and try again.");
        }
    }

    private static OperationResult<Document> Failure(string code, string message) =>
        OperationResult.Failure<Document>(
            new Diagnostic(code, DiagnosticSeverity.Error, message));
}
