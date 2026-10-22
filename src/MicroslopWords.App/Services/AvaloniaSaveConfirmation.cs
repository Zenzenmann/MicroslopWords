using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using MicroslopWords.Core.Diagnostics;

namespace MicroslopWords.App.Services;

public sealed class AvaloniaSaveConfirmation : ISaveConfirmation
{
    private readonly Window _owner;

    public AvaloniaSaveConfirmation(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public async Task<bool> ConfirmSaveDespiteUnsupportedContentAsync(
        IReadOnlyList<Diagnostic> diagnostics,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        cancellationToken.ThrowIfCancellationRequested();

        var details = string.Join(
            Environment.NewLine,
            diagnostics.Select(diagnostic => $"- {diagnostic.Message}"));

        var message =
            "This document contains content MicroslopWords cannot save yet. "
            + "Saving will omit:"
            + Environment.NewLine
            + Environment.NewLine
            + details
            + Environment.NewLine
            + Environment.NewLine
            + "The existing file is not changed unless you choose Save Anyway.";

        return await ShowAsync(
            "Unsupported content",
            message,
            acceptText: "Save Anyway",
            cancellationToken);
    }

    public async Task<bool> ConfirmAddDocxExtensionAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        var fileName = Path.GetFileName(path);
        var message =
            $"\"{fileName}\" has no file extension. Save it as \"{fileName}.docx\"?";

        return await ShowAsync(
            "Add .docx extension",
            message,
            acceptText: "Add .docx",
            cancellationToken);
    }

    private async Task<bool> ShowAsync(
        string title,
        string message,
        string acceptText,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dialog = new Window
        {
            Title = title,
            Width = 520,
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

        var acceptButton = new Button
        {
            Content = acceptText,
            MinWidth = 96,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        cancelButton.Click += (_, _) =>
        {
            completion.TrySetResult(false);
            dialog.Close();
        };

        acceptButton.Click += (_, _) =>
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
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children =
                    {
                        cancelButton,
                        acceptButton,
                    },
                },
            },
        };

        await dialog.ShowDialog(_owner);
        return await completion.Task;
    }
}
