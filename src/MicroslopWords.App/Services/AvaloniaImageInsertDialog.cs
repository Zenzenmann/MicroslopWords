using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace MicroslopWords.App.Services;

public sealed class AvaloniaImageInsertDialog : IImageInsertDialog
{
    private readonly Window _owner;

    public AvaloniaImageInsertDialog(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public async Task<ImageDialogResult?> ShowAsync(
        ImageDialogRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var dialog = new Window
        {
            Title = "Image",
            Width = 560,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var completion = new TaskCompletionSource<ImageDialogResult?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var aspectRatio = request.HeightPoints > 0
            ? request.WidthPoints / request.HeightPoints
            : 1;
        var updating = false;

        var widthBox = new TextBox
        {
            Text = request.WidthPoints.ToString("0.##", CultureInfo.InvariantCulture),
            Width = 120,
        };
        var heightBox = new TextBox
        {
            Text = request.HeightPoints.ToString("0.##", CultureInfo.InvariantCulture),
            Width = 120,
        };
        var maintainAspect = new CheckBox
        {
            Content = "Maintain aspect ratio",
            IsChecked = request.LockAspectRatio,
        };
        var altTextBox = new TextBox
        {
            Text = request.AltText,
            AcceptsReturn = false,
            PlaceholderText = "Describe the image for screen readers",
        };
        var errorText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#8A1F11")),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap,
        };
        var preview = CreatePreview(request.Data);

        void UpdateHeightFromWidth()
        {
            if (updating || maintainAspect.IsChecked != true)
            {
                return;
            }

            if (!TryParse(widthBox.Text, out var width))
            {
                return;
            }

            updating = true;
            heightBox.Text = (width / aspectRatio).ToString(
                "0.##",
                CultureInfo.InvariantCulture);
            updating = false;
        }

        void UpdateWidthFromHeight()
        {
            if (updating || maintainAspect.IsChecked != true)
            {
                return;
            }

            if (!TryParse(heightBox.Text, out var height))
            {
                return;
            }

            updating = true;
            widthBox.Text = (height * aspectRatio).ToString(
                "0.##",
                CultureInfo.InvariantCulture);
            updating = false;
        }

        widthBox.TextChanged += (_, _) => UpdateHeightFromWidth();
        heightBox.TextChanged += (_, _) => UpdateWidthFromHeight();
        maintainAspect.PropertyChanged += (_, change) =>
        {
            if (change.Property == CheckBox.IsCheckedProperty)
            {
                UpdateHeightFromWidth();
            }
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            IsCancel = true,
            MinWidth = 96,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        var acceptButton = new Button
        {
            Content = "OK",
            IsDefault = true,
            MinWidth = 96,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        cancelButton.Click += (_, _) =>
        {
            completion.TrySetResult(null);
            dialog.Close();
        };
        acceptButton.Click += (_, _) =>
        {
            if (!TryParse(widthBox.Text, out var width)
                || !TryParse(heightBox.Text, out var height))
            {
                errorText.Text = "Enter positive widths and heights in points.";
                errorText.IsVisible = true;
                return;
            }

            var lockAspect = maintainAspect.IsChecked == true;
            var effectiveHeight = lockAspect ? width / aspectRatio : height;

            if (!IsWithinLimit(width, request.MaxWidthPoints)
                || !IsWithinLimit(effectiveHeight, request.MaxHeightPoints))
            {
                errorText.Text =
                    $"Width must be at most {request.MaxWidthPoints:0.##} pt and "
                    + $"height at most {request.MaxHeightPoints:0.##} pt.";
                errorText.IsVisible = true;
                return;
            }

            completion.TrySetResult(new ImageDialogResult(
                width,
                effectiveHeight,
                lockAspect,
                altTextBox.Text?.Trim() ?? string.Empty));
            dialog.Close();
        };
        dialog.Closed += (_, _) => completion.TrySetResult(null);

        var details = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 8,
            RowSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        AddToGrid(details, new TextBlock { Text = "Width (pt)" }, 0, 0);
        AddToGrid(details, widthBox, 0, 1);
        AddToGrid(details, new TextBlock { Text = "Height (pt)" }, 0, 2);
        AddToGrid(details, heightBox, 0, 3);
        AddToGrid(details, maintainAspect, 1, 0, columnSpan: 4);

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = request.FileName,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                },
                new TextBlock
                {
                    Text = $"{request.PixelWidth} x {request.PixelHeight} pixels",
                    Foreground = new SolidColorBrush(Color.Parse("#5F6A76")),
                },
                preview,
                details,
                new TextBlock { Text = "Alt text" },
                altTextBox,
                errorText,
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

    private static Image CreatePreview(byte[] data)
    {
        var image = new Image
        {
            MaxHeight = 160,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        if (data.Length == 0)
        {
            return image;
        }

        try
        {
            using var stream = new MemoryStream(data, writable: false);
            image.Source = new Bitmap(stream);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            // The caller already validates the image; a missing preview must
            // not block the dialog.
        }

        return image;
    }

    private static void AddToGrid(
        Grid grid,
        Control control,
        int row,
        int column,
        int columnSpan = 1)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetColumnSpan(control, columnSpan);
        grid.Children.Add(control);
    }

    private static bool TryParse(string? text, out double value) =>
        double.TryParse(
            text,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value)
        && double.IsFinite(value)
        && value >= 1;

    private static bool IsWithinLimit(double value, double limit) =>
        !double.IsFinite(limit) || value <= limit + 0.001;
}
