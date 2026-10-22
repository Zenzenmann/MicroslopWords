using MicroslopWords.Core.Diagnostics;

namespace MicroslopWords.App.Services;

public interface ISaveConfirmation
{
    Task<bool> ConfirmSaveDespiteUnsupportedContentAsync(
        IReadOnlyList<Diagnostic> diagnostics,
        CancellationToken cancellationToken = default);

    Task<bool> ConfirmAddDocxExtensionAsync(
        string path,
        CancellationToken cancellationToken = default);
}
