using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace MicroslopWords.App.Services;

public sealed class AvaloniaImageFilePicker : IImageFilePicker
{
    private static readonly FilePickerFileType ImageFileType = new("Images")
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif"],
    };

    private readonly TopLevel _topLevel;

    public AvaloniaImageFilePicker(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        _topLevel = topLevel;
    }

    public async Task<string?> PickImageFileAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var files = await _topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Insert image",
                AllowMultiple = false,
                FileTypeFilter = [ImageFileType],
            });

        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }
}
