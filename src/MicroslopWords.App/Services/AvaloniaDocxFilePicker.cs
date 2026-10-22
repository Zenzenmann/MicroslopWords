using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace MicroslopWords.App.Services;

public sealed class AvaloniaDocxFilePicker : IDocxFilePicker
{
    private readonly TopLevel _topLevel;

    public AvaloniaDocxFilePicker(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        _topLevel = topLevel;
    }

    public async Task<string?> PickOpenFileAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var files = await _topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open DOCX",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Word documents")
                {
                    Patterns = ["*.docx"],
                },
            ],
        });

        if (files.Count == 0)
        {
            return null;
        }

        return files[0].TryGetLocalPath();
    }
}
