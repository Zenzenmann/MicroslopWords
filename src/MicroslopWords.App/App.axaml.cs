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
            var fontCatalog = CreateFontCatalog();
            var viewModel = new MainViewModel(
                new AvaloniaDocxFilePicker(window),
                new DocxDocumentLoader(),
                new LayoutEngine(fontCatalog),
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

    private static FontCatalog CreateFontCatalog()
    {
        var directories = Environment
            .GetEnvironmentVariable("MSW_FONT_DIRS")
            ?.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? [];

        var source = new DirectoryFontFaceSource(directories);
        var result = source.LoadFaces([]);

        return result.Faces.IsEmpty
            ? FontCatalog.Create([], diagnostics: result.Diagnostics)
            : FontCatalog.Create(result.Faces, diagnostics: result.Diagnostics);
    }
}
