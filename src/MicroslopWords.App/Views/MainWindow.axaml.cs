using Avalonia.Controls;
using MicroslopWords.App.ViewModels;

namespace MicroslopWords.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        PageScroll.SizeChanged += OnPageScrollSizeChanged;
    }

    private void OnPageScrollSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SetViewport(e.NewSize.Width, e.NewSize.Height);
        }
    }
}
