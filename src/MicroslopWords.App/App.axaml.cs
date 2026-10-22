using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MicroslopWords.App.Services;
using MicroslopWords.App.ViewModels;
using MicroslopWords.App.Views;
using MicroslopWords.Layout;

namespace MicroslopWords.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var viewModel = new MainViewModel(
                new AvaloniaDocxFilePicker(window),
                new DocxDocumentLoader(),
                new LayoutEngine(),
                new DocxDocumentSaver(),
                new AvaloniaSaveConfirmation(window),
                new RecoverySnapshotStore(),
                new AvaloniaRecoveryPrompt(window));

            window.DataContext = viewModel;
            window.Opened += async (_, _) => await viewModel.InitializeRecoveryAsync();
            window.Closed += (_, _) => viewModel.Dispose();

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
