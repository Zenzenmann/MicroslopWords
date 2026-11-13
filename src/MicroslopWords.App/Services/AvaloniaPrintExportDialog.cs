using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MicroslopWords.Rendering;

namespace MicroslopWords.App.Services;

public sealed class AvaloniaPrintExportDialog : IPrintExportDialog
{
    private readonly Window _owner;

    public AvaloniaPrintExportDialog(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public async Task<PrintExportOptions?> ShowAsync(
        PrintExportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var isPrint = request.Mode == PrintExportMode.Print;
        var dialog = new Window
        {
            Title = isPrint ? "Print" : "Export PDF",
            Width = 540,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var completion = new TaskCompletionSource<PrintExportOptions?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var allPages = new RadioButton
        {
            Content = $"All pages ({request.PageCount})",
            GroupName = "range",
            IsChecked = true,
        };
        var currentPage = new RadioButton
        {
            Content = $"Current page ({request.CurrentPage})",
            GroupName = "range",
        };
        var customRange = new RadioButton
        {
            Content = "Pages",
            GroupName = "range",
        };
        var fromBox = new TextBox
        {
            Text = "1",
            Width = 64,
            IsEnabled = false,
        };
        var toBox = new TextBox
        {
            Text = request.PageCount.ToString(CultureInfo.InvariantCulture),
            Width = 64,
            IsEnabled = false,
        };
        AutomationProperties.SetName(fromBox, "First page");
        AutomationProperties.SetName(toBox, "Last page");
        customRange.IsCheckedChanged += (_, _) =>
        {
            fromBox.IsEnabled = customRange.IsChecked == true;
            toBox.IsEnabled = customRange.IsChecked == true;
        };

        var copiesBox = new TextBox
        {
            Text = "1",
            Width = 64,
        };
        AutomationProperties.SetName(copiesBox, "Copies");

        var printerChoices = new List<string> { "Default printer" };
        printerChoices.AddRange(request.Printers);
        var printerBox = new ComboBox
        {
            Width = 260,
            ItemsSource = printerChoices,
            SelectedIndex = 0,
        };
        AutomationProperties.SetName(printerBox, "Printer");

        var includeHeaders = new CheckBox
        {
            Content = "Include headers and footers",
            IsChecked = true,
        };
        var summaryText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#5F6A76")),
            TextWrapping = TextWrapping.Wrap,
        };
        var errorText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#8A1F11")),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap,
        };

        void UpdateSummary()
        {
            var selected = ReadRange(
                allPages,
                currentPage,
                customRange,
                fromBox,
                toBox,
                request.CurrentPage,
                out _);

            if (selected is null)
            {
                summaryText.Text = "Choose a valid page range.";
                return;
            }

            var resolved = selected.Resolve(request.PageCount);
            summaryText.Text = resolved.IsSuccess
                ? resolved.Value.Length == request.PageCount
                    ? $"All {request.PageCount} pages will be produced."
                    : $"{resolved.Value.Length} page(s) will be produced: "
                        + $"{string.Join(", ", resolved.Value)}."
                : "Choose a valid page range.";
        }

        allPages.IsCheckedChanged += (_, _) => UpdateSummary();
        currentPage.IsCheckedChanged += (_, _) => UpdateSummary();
        customRange.IsCheckedChanged += (_, _) => UpdateSummary();
        fromBox.TextChanged += (_, _) => UpdateSummary();
        toBox.TextChanged += (_, _) => UpdateSummary();
        UpdateSummary();

        var cancelButton = new Button
        {
            Content = "Cancel",
            IsCancel = true,
            MinWidth = 96,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        var acceptButton = new Button
        {
            Content = isPrint ? "Print" : "Export",
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
            var range = ReadRange(
                allPages,
                currentPage,
                customRange,
                fromBox,
                toBox,
                request.CurrentPage,
                out var error);

            if (range is null)
            {
                errorText.Text = error;
                errorText.IsVisible = true;
                return;
            }

            var resolved = range.Resolve(request.PageCount);

            if (!resolved.IsSuccess)
            {
                errorText.Text = resolved.Diagnostics.FirstOrDefault()?.Message
                    ?? "Choose a valid page range.";
                errorText.IsVisible = true;
                return;
            }

            var copies = 1;

            if (isPrint
                && (!int.TryParse(
                        copiesBox.Text,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out copies)
                    || copies is < 1 or > 99))
            {
                errorText.Text = "Choose between 1 and 99 copies.";
                errorText.IsVisible = true;
                return;
            }

            var printerName = isPrint && printerBox.SelectedIndex > 0
                ? printerBox.SelectedItem as string
                : null;

            completion.TrySetResult(new PrintExportOptions(
                range,
                includeHeaders.IsChecked == true,
                copies,
                printerName));
            dialog.Close();
        };
        dialog.Closed += (_, _) => completion.TrySetResult(null);

        var rangeGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            ColumnSpacing = 8,
            RowSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        AddToGrid(rangeGrid, allPages, 0, 0, columnSpan: 4);
        AddToGrid(rangeGrid, currentPage, 1, 0, columnSpan: 4);
        AddToGrid(rangeGrid, customRange, 2, 0);
        AddToGrid(rangeGrid, fromBox, 2, 1);
        AddToGrid(rangeGrid, new TextBlock { Text = "to", VerticalAlignment = VerticalAlignment.Center }, 2, 2);
        AddToGrid(rangeGrid, toBox, 2, 3);

        var printOptions = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 8,
            RowSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsVisible = isPrint,
        };
        AddToGrid(printOptions, new TextBlock { Text = "Copies", VerticalAlignment = VerticalAlignment.Center }, 0, 0);
        AddToGrid(printOptions, copiesBox, 0, 1);
        AddToGrid(printOptions, new TextBlock { Text = "Printer", VerticalAlignment = VerticalAlignment.Center }, 1, 0);
        AddToGrid(printOptions, printerBox, 1, 1);

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = request.DocumentTitle,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                },
                new TextBlock
                {
                    Text = $"{request.PageCount} pages - output matches the editor pagination.",
                    Foreground = new SolidColorBrush(Color.Parse("#5F6A76")),
                },
                rangeGrid,
                summaryText,
                printOptions,
                includeHeaders,
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

    private static PageRange? ReadRange(
        RadioButton allPages,
        RadioButton currentPage,
        RadioButton customRange,
        TextBox fromBox,
        TextBox toBox,
        int current,
        out string error)
    {
        error = string.Empty;

        if (allPages.IsChecked == true)
        {
            return new PageRange(PageRangeKind.All);
        }

        if (currentPage.IsChecked == true)
        {
            return new PageRange(PageRangeKind.Current, CurrentPage: current);
        }

        if (!int.TryParse(
                fromBox.Text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var from)
            || !int.TryParse(
                toBox.Text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var to))
        {
            error = "Enter whole page numbers.";
            return null;
        }

        return new PageRange(PageRangeKind.Custom, FromPage: from, ToPage: to);
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
}
