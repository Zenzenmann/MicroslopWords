using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MicroslopWords.App.Services;

public sealed class AvaloniaRecoveryPrompt : IRecoveryPrompt
{
    private readonly Window _owner;

    public AvaloniaRecoveryPrompt(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public async Task<RecoveryPromptChoice> PromptAsync(
        RecoverySnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();

        var dialog = CreateDialog("Recover unsaved draft");
        var completion = new TaskCompletionSource<RecoveryPromptChoice>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var laterButton = CreateButton("Later", isDefault: true, isCancel: true);
        var discardButton = CreateButton("Discard", isDefault: false, isCancel: false);
        var openButton = CreateButton("Open recovered draft", isDefault: false, isCancel: false);
        openButton.IsEnabled = snapshot.IsReadable;

        laterButton.Click += (_, _) =>
        {
            completion.TrySetResult(RecoveryPromptChoice.Later);
            dialog.Close();
        };

        discardButton.Click += (_, _) =>
        {
            completion.TrySetResult(RecoveryPromptChoice.Discard);
            dialog.Close();
        };

        openButton.Click += (_, _) =>
        {
            completion.TrySetResult(RecoveryPromptChoice.OpenRecoveredDraft);
            dialog.Close();
        };

        dialog.Closed += (_, _) => completion.TrySetResult(RecoveryPromptChoice.Later);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = BuildMessage(snapshot),
                    TextWrapping = TextWrapping.Wrap,
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children =
                    {
                        laterButton,
                        discardButton,
                        openButton,
                    },
                },
            },
        };

        await dialog.ShowDialog(_owner);
        return await completion.Task;
    }

    public async Task<bool> ConfirmDiscardAsync(
        RecoverySnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();

        var dialog = CreateDialog("Discard recovered draft");
        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var cancelButton = CreateButton("Cancel", isDefault: true, isCancel: true);
        var discardButton = CreateButton("Discard", isDefault: false, isCancel: false);

        cancelButton.Click += (_, _) =>
        {
            completion.TrySetResult(false);
            dialog.Close();
        };

        discardButton.Click += (_, _) =>
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
                    Text =
                        $"Discard the recovery snapshot for \"{snapshot.Title}\"? "
                        + "This cannot be undone.",
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
                        discardButton,
                    },
                },
            },
        };

        await dialog.ShowDialog(_owner);
        return await completion.Task;
    }

    private static Window CreateDialog(string title) =>
        new()
        {
            Title = title,
            Width = 560,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

    private static Button CreateButton(string text, bool isDefault, bool isCancel) =>
        new()
        {
            Content = text,
            IsDefault = isDefault,
            IsCancel = isCancel,
            MinWidth = 96,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

    private static string BuildMessage(RecoverySnapshot snapshot)
    {
        var sourcePath = string.IsNullOrWhiteSpace(snapshot.SourcePath)
            ? "Not saved to a file"
            : snapshot.SourcePath;
        var lines = new List<string>
        {
            "MicroslopWords found an unsaved recovery snapshot.",
            string.Empty,
            $"Document: {snapshot.Title}",
            $"Last snapshot: {snapshot.Timestamp.ToLocalTime():g}",
            $"Original file: {sourcePath}",
        };

        if (!snapshot.IsReadable)
        {
            lines.Add(string.Empty);
            lines.Add(snapshot.ErrorMessage);
        }

        lines.Add(string.Empty);
        lines.Add(
            "Open the recovered draft, discard it, or choose Later to decide next time.");
        return string.Join(Environment.NewLine, lines);
    }
}
