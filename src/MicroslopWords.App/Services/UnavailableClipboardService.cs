namespace MicroslopWords.App.Services;

public sealed class UnavailableClipboardService : IClipboardService
{
    public static UnavailableClipboardService Instance { get; } = new();

    public Task<ClipboardContent?> ReadAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ClipboardContent?>(null);

    public Task<bool> WriteAsync(
        ClipboardContent content,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
