using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;

namespace MicroslopWords.App.Services;

public interface IDocxDocumentSaver
{
    OperationResult<string> Save(
        Document document,
        string path,
        CancellationToken cancellationToken = default);
}
