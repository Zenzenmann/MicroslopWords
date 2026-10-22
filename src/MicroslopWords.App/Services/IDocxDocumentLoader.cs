using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;

namespace MicroslopWords.App.Services;

public interface IDocxDocumentLoader
{
    OperationResult<Document> Load(string path, CancellationToken cancellationToken = default);
}
