using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using MicroslopWords.Formats.Docx;
using MicroslopWords.Rendering;

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
                var result = DocxService.Load(stream, title, cancellationToken);

                return result.IsSuccess && result.Value is not null
                    ? AddImageDecodeDiagnostics(result)
                    : result;
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

    private static OperationResult<Document> AddImageDecodeDiagnostics(
        OperationResult<Document> result)
    {
        var diagnostics = result.Diagnostics.ToList();
        var inspected = new HashSet<string>(StringComparer.Ordinal);

        foreach (var image in result.Value!.Sections
                     .SelectMany(section => section.Blocks)
                     .OfType<ParagraphBlock>()
                     .SelectMany(paragraph => paragraph.Runs)
                     .Select(run => run.EffectiveImage)
                     .Where(image => image is not null && !image.IsPlaceholder)
                     .Select(image => image!))
        {
            var key = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(image.Data.AsSpan()));

            if (!inspected.Add(key))
            {
                continue;
            }

            if (ImageDecoder.Validate(image.Data.ToArray()).IsSuccess)
            {
                continue;
            }

            diagnostics.Add(new Diagnostic(
                DocxDiagnosticCodes.UnsupportedImage,
                DiagnosticSeverity.Warning,
                "An embedded image is damaged and is shown as a placeholder."));
            break;
        }

        return OperationResult.Success(result.Value, diagnostics);
    }

    private static OperationResult<Document> Failure(string code, string message) =>
        OperationResult.Failure<Document>(
            new Diagnostic(code, DiagnosticSeverity.Error, message));
}
