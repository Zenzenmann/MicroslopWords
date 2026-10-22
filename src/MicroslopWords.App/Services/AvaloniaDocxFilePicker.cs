using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace MicroslopWords.App.Services;

public sealed class AvaloniaDocxFilePicker : IDocxFilePicker
{
    private static readonly FilePickerFileType DocxFileType = new("Word documents")
    {
        Patterns = ["*.docx"],
    };

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
            FileTypeFilter = [DocxFileType],
        });

        if (files.Count == 0)
        {
            return null;
        }

        return files[0].TryGetLocalPath();
    }

    public async Task<string?> PickSaveFileAsync(
        string suggestedFileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var file = await _topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save DOCX",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "docx",
            ShowOverwritePrompt = true,
            FileTypeChoices = [DocxFileType],
        });

        return file?.TryGetLocalPath();
    }
}
