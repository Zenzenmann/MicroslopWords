using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using MicroslopWords.App.ViewModels;
using MicroslopWords.App.Views;
using Shouldly;

[assembly: AvaloniaTestApplication(typeof(MicroslopWords.Ui.Tests.TestAppBuilder))]

namespace MicroslopWords.Ui.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<MicroslopWords.App.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public sealed class MainWindowTests
{
    [AvaloniaFact]
    public void MainWindow_uses_main_view_model_and_title()
    {
        var viewModel = new MainViewModel();
        var window = new MainWindow { DataContext = viewModel };

        window.Title.ShouldBe("MicroslopWords");
        window.DataContext.ShouldBeSameAs(viewModel);
    }
}
