namespace MicroslopWords.App.Services;

public sealed class UnavailableImageInsertDialog : IImageInsertDialog
{
    public Task<ImageDialogResult?> ShowAsync(
        ImageDialogRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ImageDialogResult?>(null);
}
