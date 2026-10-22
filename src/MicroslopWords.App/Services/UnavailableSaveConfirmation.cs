using MicroslopWords.Core.Diagnostics;

namespace MicroslopWords.App.Services;

public sealed class UnavailableSaveConfirmation : ISaveConfirmation
{
    public Task<bool> ConfirmSaveDespiteUnsupportedContentAsync(
        IReadOnlyList<Diagnostic> diagnostics,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> ConfirmAddDocxExtensionAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
