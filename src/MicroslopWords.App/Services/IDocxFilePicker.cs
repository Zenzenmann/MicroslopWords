namespace MicroslopWords.App.Services;

public interface IDocxFilePicker
{
    Task<string?> PickOpenFileAsync(CancellationToken cancellationToken = default);

    Task<string?> PickSaveFileAsync(
        string suggestedFileName,
        CancellationToken cancellationToken = default);
}
