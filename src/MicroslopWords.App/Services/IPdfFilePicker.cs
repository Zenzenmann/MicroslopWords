namespace MicroslopWords.App.Services;

public interface IPdfFilePicker
{
    Task<string?> PickSaveFileAsync(
        string suggestedFileName,
        CancellationToken cancellationToken = default);
}
