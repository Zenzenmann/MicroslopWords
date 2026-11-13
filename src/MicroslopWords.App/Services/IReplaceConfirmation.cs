namespace MicroslopWords.App.Services;

public interface IReplaceConfirmation
{
    Task<bool> ConfirmReplaceAllAsync(
        int count,
        CancellationToken cancellationToken = default);
}
