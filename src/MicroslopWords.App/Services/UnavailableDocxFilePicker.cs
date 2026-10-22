namespace MicroslopWords.App.Services;

public sealed class UnavailableDocxFilePicker : IDocxFilePicker
{
    public Task<string?> PickOpenFileAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public Task<string?> PickSaveFileAsync(
        string suggestedFileName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}
