using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MicroslopWords.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string DocumentTitle { get; set; } = "Untitled";

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Ready";

    [ObservableProperty]
    public partial bool IsModified { get; set; }

    [RelayCommand]
    private void NewDocument()
    {
        DocumentTitle = "Untitled";
        IsModified = false;
        StatusText = "New document created.";
    }

    [RelayCommand]
    private void OpenDocument() => StatusText = "Open is planned for STORY-006.";

    [RelayCommand]
    private void SaveDocument()
    {
        IsModified = false;
        StatusText = $"Saved {DocumentTitle}.docx";
    }

    [RelayCommand]
    private void ToggleBold() => StatusText = "Bold is planned for STORY-007.";

    [RelayCommand]
    private void ToggleItalic() => StatusText = "Italic is planned for STORY-007.";

    [RelayCommand]
    private void ToggleUnderline() => StatusText = "Underline is planned for STORY-007.";

    [RelayCommand]
    private void Find() => StatusText = "Find is planned for STORY-008.";
}
