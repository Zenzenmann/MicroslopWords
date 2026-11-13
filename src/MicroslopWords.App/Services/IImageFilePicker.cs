namespace MicroslopWords.App.Services;

public interface IImageFilePicker
{
    Task<string?> PickImageFileAsync(CancellationToken cancellationToken = default);
}
