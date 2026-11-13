using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MicroslopWords.App.Services;

public sealed class AvaloniaReplaceConfirmation : IReplaceConfirmation
{
    private readonly Window _owner;

    public AvaloniaReplaceConfirmation(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public async Task<bool> ConfirmReplaceAllAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (count <= 0)
        {
            return false;
        }

        var occurrence = count == 1 ? "occurrence" : "occurrences";
        var dialog = new Window
        {
            Title = "Replace all",
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelButton = new Button
        {
            Content = "Cancel",
            IsDefault = true,
            IsCancel = true,
            MinWidth = 96,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        var replaceButton = new Button
        {
            Content = "Replace all",
            MinWidth = 96,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        cancelButton.Click += (_, _) =>
        {
            completion.TrySetResult(false);
            dialog.Close();
        };
        replaceButton.Click += (_, _) =>
        {
            completion.TrySetResult(true);
            dialog.Close();
        };
        dialog.Closed += (_, _) => completion.TrySetResult(false);

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = $"Replace {count} {occurrence}?",
                    TextWrapping = TextWrapping.Wrap,
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children =
                    {
                        cancelButton,
                        replaceButton,
                    },
                },
            },
        };

        await dialog.ShowDialog(_owner);
        return await completion.Task;
    }
}
