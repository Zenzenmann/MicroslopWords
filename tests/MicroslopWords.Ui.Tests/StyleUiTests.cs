using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MicroslopWords.App.ViewModels;
using MicroslopWords.App.Views;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Styles;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class StyleUiTests
{
    [Fact]
    public void MainViewModel_lists_builtin_styles_and_applies_heading()
    {
        var viewModel = new MainViewModel();
        viewModel.StyleItems.ShouldContain(item => item.Name == "Normal");
        viewModel.StyleItems.ShouldContain(item => item.Name == "Title");
        viewModel.StyleItems.ShouldContain(item => item.Name == "Heading 1");
        viewModel.StyleItems.ShouldContain(item => item.Name == "Default");
        viewModel.SelectAllCommand.Execute(null);

        var heading = viewModel.StyleItems.Single(item =>
            item.Kind == "Paragraph" && item.Name == "Heading 1");
        viewModel.ApplyStyle(heading);

        var paragraph = viewModel.Document.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .Single();

        paragraph.Properties!.StyleId.ShouldBe("Heading1");
        viewModel.CurrentParagraphStyleName.ShouldBe("Heading 1");
        viewModel.CanUndo.ShouldBeTrue();
    }

    [Fact]
    public void MainViewModel_rejects_duplicate_style_name_case_insensitively()
    {
        var viewModel = new MainViewModel();
        var count = viewModel.StyleItems.Count;
        viewModel.NewStyleCommand.Execute(null);
        viewModel.StyleEditorName = "heading 1";

        viewModel.SaveStyleCommand.Execute(null);

        viewModel.ErrorMessage.ShouldContain("already exists");
        viewModel.IsStyleEditorOpen.ShouldBeTrue();
        viewModel.StyleItems.Count.ShouldBe(count);
    }

    [Fact]
    public void MainViewModel_creates_and_deletes_a_style()
    {
        var viewModel = new MainViewModel();
        viewModel.NewStyleCommand.Execute(null);
        viewModel.StyleEditorName = "Project heading";
        viewModel.StyleEditorKind = "Paragraph";

        viewModel.SaveStyleCommand.Execute(null);

        var created = viewModel.StyleItems.Single(item => item.Name == "Project heading");
        viewModel.Document.EffectiveStyles.FindParagraphStyle(created.Id).ShouldNotBeNull();
        viewModel.DeleteStyleCommand.Execute(null);
        viewModel.IsStyleDeleteConfirmationOpen.ShouldBeTrue();

        viewModel.ConfirmDeleteStyleCommand.Execute(null);

        viewModel.IsStyleDeleteConfirmationOpen.ShouldBeFalse();
        viewModel.Document.EffectiveStyles.FindParagraphStyle(created.Id).ShouldBeNull();
        var paragraph = viewModel.Document.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .Single();
        paragraph.Properties!.StyleId.ShouldBe(StyleSheet.NormalStyleId);
    }

    [Fact]
    public void MainViewModel_toggles_styles_panel()
    {
        var viewModel = new MainViewModel();

        viewModel.IsStylesPanelOpen.ShouldBeFalse();
        viewModel.ToggleStylesPanelCommand.Execute(null);
        viewModel.IsStylesPanelOpen.ShouldBeTrue();
        viewModel.ToggleStylesPanelCommand.Execute(null);
        viewModel.IsStylesPanelOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void MainWindow_binds_style_picker_and_styles_panel()
    {
        var viewModel = new MainViewModel();
        var window = new MainWindow { DataContext = viewModel };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var picker = window.FindControl<ComboBox>("StylePicker");
        var panel = window.FindControl<Border>("StylesPanel");

        picker.ShouldNotBeNull();
        panel.ShouldNotBeNull();
        picker.ItemCount.ShouldBe(viewModel.StyleItems.Count);
        AutomationProperties.GetName(picker).ShouldBe("Paragraph style");
        panel.IsVisible.ShouldBeFalse();

        viewModel.ToggleStylesPanelCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        panel.IsVisible.ShouldBeTrue();

        window.Close();
    }
}
