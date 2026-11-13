namespace MicroslopWords.App.Services;

public sealed class UnavailablePdfFilePicker : IPdfFilePicker
{
    public Task<string?> PickSaveFileAsync(
        string suggestedFileName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}
