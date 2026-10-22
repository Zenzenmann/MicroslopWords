using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using MicroslopWords.Formats.Docx;

namespace MicroslopWords.App.Services;

public sealed class DocxDocumentSaver : IDocxDocumentSaver
{
    private readonly IAtomicFileWriter _fileWriter;

    public DocxDocumentSaver()
        : this(new AtomicFileWriter())
    {
    }

    public DocxDocumentSaver(IAtomicFileWriter fileWriter)
    {
        ArgumentNullException.ThrowIfNull(fileWriter);
        _fileWriter = fileWriter;
    }

    public OperationResult<string> Save(
        Document document,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(path))
        {
            return Failure(
                DocxDiagnosticCodes.InvalidSavePath,
                "Choose a destination for the document.");
        }

        try
        {
            _fileWriter.Write(
                path,
                stream =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    DocxService.Save(document, stream);
                    cancellationToken.ThrowIfCancellationRequested();
                },
                cancellationToken);

            return OperationResult.Success(path);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(
                DocxDiagnosticCodes.SaveDenied,
                "The destination cannot be written. Check its permissions and try again.");
        }
        catch (System.Security.SecurityException)
        {
            return Failure(
                DocxDiagnosticCodes.SaveDenied,
                "The destination cannot be written. Check its permissions and try again.");
        }
        catch (IOException)
        {
            return Failure(
                DocxDiagnosticCodes.SaveFailed,
                "The document could not be saved. Close the file if it is open and try again.");
        }
        catch (NotSupportedException)
        {
            return Failure(
                DocxDiagnosticCodes.InvalidSavePath,
                "The destination path is not supported. Choose another location.");
        }
        catch (ArgumentException)
        {
            return Failure(
                DocxDiagnosticCodes.InvalidSavePath,
                "The destination path is not valid. Choose another location.");
        }
    }

    private static OperationResult<string> Failure(string code, string message) =>
        OperationResult.Failure<string>(
            new Diagnostic(code, DiagnosticSeverity.Error, message));
}
