using MicroslopWords.Core.Documents;

namespace MicroslopWords.App.Services;

public sealed record ClipboardContent(
    string Text,
    string? Html = null,
    DocumentFragment? Fragment = null,
    bool HasImage = false,
    byte[]? ImagePng = null);

public interface IClipboardService
{
    Task<ClipboardContent?> ReadAsync(CancellationToken cancellationToken = default);

    Task<bool> WriteAsync(
        ClipboardContent content,
        CancellationToken cancellationToken = default);
}
