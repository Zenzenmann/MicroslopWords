using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using MicroslopWords.Core.Documents;

namespace MicroslopWords.App.Services;

public sealed class AvaloniaClipboardService : IClipboardService
{
    private static readonly DataFormat<string> HtmlFormat =
        DataFormat.CreateStringPlatformFormat("text/html");

    private static readonly DataFormat<string> WindowsHtmlFormat =
        DataFormat.CreateStringPlatformFormat("HTML Format");

    private readonly Window _window;
    private string? _lastText;
    private DocumentFragment? _lastFragment;

    public AvaloniaClipboardService(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
    }

    public async Task<ClipboardContent?> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        var clipboard = GetClipboard();

        if (clipboard is null)
        {
            return null;
        }

        try
        {
            var formats = await clipboard.GetDataFormatsAsync();
            var text = await clipboard.TryGetTextAsync()
                ?? string.Empty;
            var html = await TryGetHtmlAsync(clipboard)
                ?? await TryGetHtmlAsync(clipboard, WindowsHtmlFormat);
            var hasImage = formats.Any(format => format == DataFormat.Bitmap);
            var fragment = _lastFragment is not null
                && string.Equals(text, _lastText, StringComparison.Ordinal)
                    ? _lastFragment
                    : null;

            return new ClipboardContent(
                text,
                html,
                fragment,
                hasImage);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException)
        {
            return null;
        }
    }

    public async Task<bool> WriteAsync(
        ClipboardContent content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var clipboard = GetClipboard();

        if (clipboard is null)
        {
            return false;
        }

        try
        {
            var item = DataTransferItem.CreateText(content.Text);

            if (!string.IsNullOrWhiteSpace(content.Html))
            {
                item.Set(HtmlFormat, content.Html);

                if (OperatingSystem.IsWindows())
                {
                    item.Set(WindowsHtmlFormat, BuildCfHtml(content.Html));
                }
            }

            var transfer = new DataTransfer();
            transfer.Add(item);
            await clipboard.SetDataAsync(transfer);
            _lastText = content.Text;
            _lastFragment = content.Fragment;

            return true;
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException)
        {
            try
            {
                await clipboard.SetTextAsync(content.Text);
                _lastText = content.Text;
                _lastFragment = content.Fragment;

                return true;
            }
            catch (Exception fallbackException) when (
                fallbackException is not OperationCanceledException)
            {
                return false;
            }
        }
    }

    private Avalonia.Input.Platform.IClipboard? GetClipboard() =>
        TopLevel.GetTopLevel(_window)?.Clipboard;

    private static async Task<string?> TryGetHtmlAsync(
        Avalonia.Input.Platform.IClipboard clipboard,
        DataFormat<string>? format = null)
    {
        try
        {
            var value = await clipboard.TryGetValueAsync(format ?? HtmlFormat);

            return value is null ? null : ExtractHtmlFragment(value);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException)
        {
            return null;
        }
    }

    private static string ExtractHtmlFragment(string html)
    {
        const string startMarker = "<!--StartFragment-->";
        const string endMarker = "<!--EndFragment-->";

        var start = html.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);

        if (start < 0)
        {
            return html;
        }

        start += startMarker.Length;
        var end = html.IndexOf(endMarker, start, StringComparison.OrdinalIgnoreCase);

        return end < 0 ? html[start..] : html[start..end];
    }

    private static string BuildCfHtml(string fragment)
    {
        const string opening = "<html><body><!--StartFragment-->";
        const string closing = "<!--EndFragment--></body></html>";
        const string placeholderHeader =
            "Version:0.9\r\n"
            + "StartHTML:0000000000\r\n"
            + "EndHTML:0000000000\r\n"
            + "StartFragment:0000000000\r\n"
            + "EndFragment:0000000000\r\n";

        var startHtml = placeholderHeader.Length;
        var startFragment = startHtml + opening.Length;
        var endFragment = startFragment + fragment.Length;
        var endHtml = endFragment + closing.Length;

        return $"Version:0.9\r\n"
            + $"StartHTML:{startHtml:D10}\r\n"
            + $"EndHTML:{endHtml:D10}\r\n"
            + $"StartFragment:{startFragment:D10}\r\n"
            + $"EndFragment:{endFragment:D10}\r\n"
            + opening
            + fragment
            + closing;
    }
}
