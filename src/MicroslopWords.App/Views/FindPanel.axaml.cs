using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using MicroslopWords.App.Controls;
using MicroslopWords.App.ViewModels;

namespace MicroslopWords.App.Views;

public partial class FindPanel : UserControl
{
    private MainViewModel? _viewModel;

    public FindPanel()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _viewModel = DataContext as MainViewModel;

            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            }
        };

        SearchBox.KeyDown += OnSearchKeyDown;
        ReplaceBox.KeyDown += OnReplaceKeyDown;
    }

    protected override void OnPropertyChanged(
        Avalonia.AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsVisibleProperty)
        {
            if (IsVisible)
            {
                ScheduleSearchFocus();
            }
            else
            {
                ScheduleCanvasFocus();
            }
        }
    }

    private void OnViewModelPropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.FindFocusRequest))
        {
            ScheduleSearchFocus();
        }
        else if (e.PropertyName == nameof(MainViewModel.IsReplaceRowVisible)
            && _viewModel?.IsReplaceRowVisible == true)
        {
            Dispatcher.UIThread.Post(
                () => ReplaceBox.Focus(),
                DispatcherPriority.Input);
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                _viewModel.FindPreviousCommand.Execute(null);
            }
            else
            {
                _viewModel.FindNextCommand.Execute(null);
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _viewModel.CloseFindCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnReplaceKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            _viewModel.ReplaceCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _viewModel.CloseFindCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void ScheduleSearchFocus() =>
        Dispatcher.UIThread.Post(
            () =>
            {
                if (!IsVisible)
                {
                    return;
                }

                SearchBox.Focus();
                SearchBox.SelectAll();
            },
            DispatcherPriority.Input);

    private void ScheduleCanvasFocus() =>
        Dispatcher.UIThread.Post(
            () =>
            {
                if (TopLevel.GetTopLevel(this) is MainWindow window)
                {
                    window.FindControl<DocumentPreviewControl>("PageCanvas")
                        ?.Focus();
                }
            },
            DispatcherPriority.Input);
}
