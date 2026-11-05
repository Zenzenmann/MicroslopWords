using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MicroslopWords.App.Controls;
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

        window.Title.ShouldBe("Untitled - MicroslopWords");
        window.DataContext.ShouldBeSameAs(viewModel);
    }

    [AvaloniaFact]
    public void MainWindow_wires_the_editable_canvas_to_the_view_model()
    {
        var viewModel = new MainViewModel();
        var window = new MainWindow { DataContext = viewModel };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var canvas = window.FindControl<DocumentPreviewControl>("PageCanvas");

        canvas.ShouldNotBeNull();
        canvas.Document.ShouldBeSameAs(viewModel.Document);
        canvas.IsEditable.ShouldBeTrue();
        canvas.EditorHost.ShouldBeSameAs(viewModel);

        canvas.CommitText("Hello").ShouldBeTrue();
        Dispatcher.UIThread.RunJobs();

        viewModel.Document.PlainText.ShouldBe("Hello");
        viewModel.Selection.Focus.Offset.ShouldBe(5);
        canvas.Document.ShouldBeSameAs(viewModel.Document);

        window.Close();
    }

    [AvaloniaFact]
    public void MainWindow_binds_formatting_toolbar_state_and_accessible_names()
    {
        var viewModel = new MainViewModel();
        var window = new MainWindow { DataContext = viewModel };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var boldToggle = window.FindControl<ToggleButton>("BoldToggle");

        boldToggle.ShouldNotBeNull();
        AutomationProperties.GetName(boldToggle).ShouldBe("Bold");

        viewModel.InsertText("Hello");
        Dispatcher.UIThread.RunJobs();
        boldToggle.IsChecked.ShouldBe(false);

        viewModel.SelectAllCommand.Execute(null);
        viewModel.ToggleBoldCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        boldToggle.IsChecked.ShouldBe(true);

        window.Close();
    }
}
