namespace MicroslopWords.App.Services;

public sealed class UnavailableImageFilePicker : IImageFilePicker
{
    public Task<string?> PickImageFileAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}
