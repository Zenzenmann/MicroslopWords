using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace MicroslopWords.App.Services;

public sealed class AvaloniaPdfFilePicker : IPdfFilePicker
{
    private static readonly FilePickerFileType PdfFileType = new("PDF documents")
    {
        Patterns = ["*.pdf"],
        MimeTypes = ["application/pdf"],
    };

    private readonly TopLevel _topLevel;

    public AvaloniaPdfFilePicker(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        _topLevel = topLevel;
    }

    public async Task<string?> PickSaveFileAsync(
        string suggestedFileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var file = await _topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Export PDF",
                SuggestedFileName = suggestedFileName,
                DefaultExtension = "pdf",
                ShowOverwritePrompt = true,
                FileTypeChoices = [PdfFileType],
            });

        return file?.TryGetLocalPath();
    }
}
