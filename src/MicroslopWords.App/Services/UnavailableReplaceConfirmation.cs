namespace MicroslopWords.App.Services;

public sealed class UnavailableReplaceConfirmation : IReplaceConfirmation
{
    public Task<bool> ConfirmReplaceAllAsync(
        int count,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
