using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicroslopWords.App.Services;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;

namespace MicroslopWords.App.ViewModels;

public partial class MainViewModel
{
    private ImmutableArray<SearchMatch> _searchMatches = [];
    private IReadOnlyList<Selection> _searchHighlights = [];
    private int _activeSearchMatchIndex = -1;
    private string _findNotice = string.Empty;

    [ObservableProperty]
    private bool _isFindPanelOpen;

    [ObservableProperty]
    private bool _isReplaceRowVisible;

    [ObservableProperty]
    private string _findQuery = string.Empty;

    [ObservableProperty]
    private string _replaceQuery = string.Empty;

    [ObservableProperty]
    private bool _matchCase;

    [ObservableProperty]
    private bool _wholeWords;

    [ObservableProperty]
    private int _findFocusRequest;

    public IReplaceConfirmation ReplaceConfirmation { get; set; } =
        new UnavailableReplaceConfirmation();

    public IReadOnlyList<Selection> SearchHighlights => _searchHighlights;

    public bool HasSearchMatches => _searchMatches.Length > 0;

    public int FindMatchCount => _searchMatches.Length;

    public string FindStatusText
    {
        get
        {
            if (!IsFindPanelOpen)
            {
                return string.Empty;
            }

            if (FindQuery.Length == 0)
            {
                return "Type to search the document body.";
            }

            if (_searchMatches.Length == 0)
            {
                return $"No matches for \"{FindQuery}\".";
            }

            var status = _activeSearchMatchIndex >= 0
                ? $"Match {_activeSearchMatchIndex + 1} of {_searchMatches.Length}."
                : _searchMatches.Length == 1
                    ? "1 match."
                    : $"{_searchMatches.Length} matches.";

            return string.IsNullOrEmpty(_findNotice)
                ? status
                : $"{status} {_findNotice}";
        }
    }

    internal void OpenFindPanel() => ShowFindPanel(replace: false);

    [RelayCommand]
    private void OpenReplace() => ShowFindPanel(replace: true);

    [RelayCommand]
    private void FindNext()
    {
        if (_searchMatches.Length == 0)
        {
            return;
        }

        if (_activeSearchMatchIndex >= 0)
        {
            var next = (_activeSearchMatchIndex + 1) % _searchMatches.Length;

            MoveToMatch(next, wrapped: next == 0);
            return;
        }

        var index = FindForwardIndex();

        if (index < 0)
        {
            MoveToMatch(0, wrapped: true);
        }
        else
        {
            MoveToMatch(index, wrapped: false);
        }
    }

    [RelayCommand]
    private void FindPrevious()
    {
        if (_searchMatches.Length == 0)
        {
            return;
        }

        if (_activeSearchMatchIndex >= 0)
        {
            var previous =
                (_activeSearchMatchIndex - 1 + _searchMatches.Length)
                % _searchMatches.Length;

            MoveToMatch(previous, wrapped: previous == _searchMatches.Length - 1);
            return;
        }

        var index = FindBackwardIndex();

        if (index < 0)
        {
            MoveToMatch(_searchMatches.Length - 1, wrapped: true);
        }
        else
        {
            MoveToMatch(index, wrapped: false);
        }
    }

    [RelayCommand]
    private void CloseFind()
    {
        if (_activeSearchMatchIndex >= 0
            && _activeSearchMatchIndex < _searchMatches.Length)
        {
            Selection = SearchEngine.GetSelection(
                Document,
                _searchMatches[_activeSearchMatchIndex]);
        }

        IsFindPanelOpen = false;
    }

    [RelayCommand]
    private void Replace()
    {
        if (!IsEditingEnabled || _searchMatches.Length == 0)
        {
            return;
        }

        var index = _activeSearchMatchIndex >= 0
            && _activeSearchMatchIndex < _searchMatches.Length
            ? _activeSearchMatchIndex
            : 0;
        var match = _searchMatches[index];

        ExecuteEdit(new ReplaceMatchCommand(
            _history.Selection,
            match,
            ReplaceQuery));

        RefreshSearchResults(resetActive: true);
        SelectMatchAfterCaret();
        StatusText = "Replaced 1 match.";
    }

    [RelayCommand]
    private async Task ReplaceAllAsync()
    {
        if (!IsEditingEnabled || _searchMatches.Length == 0)
        {
            return;
        }

        var count = _searchMatches.Length;

        if (!await ReplaceConfirmation.ConfirmReplaceAllAsync(count))
        {
            return;
        }

        ExecuteEdit(new MicroslopWords.Core.Editing.ReplaceAllCommand(
            _history.Selection,
            FindQuery,
            ReplaceQuery,
            CurrentFindOptions));

        RefreshSearchResults(resetActive: true);
        StatusText = $"Replaced {count} {(count == 1 ? "match" : "matches")}.";
        UpdateFindStatus();
    }

    partial void OnIsFindPanelOpenChanged(bool value)
    {
        if (!value)
        {
            ClearSearchResults();
        }
    }

    partial void OnFindQueryChanged(string value) =>
        RefreshSearchResults(resetActive: true);

    partial void OnMatchCaseChanged(bool value) =>
        RefreshSearchResults(resetActive: true);

    partial void OnWholeWordsChanged(bool value) =>
        RefreshSearchResults(resetActive: true);

    private FindOptions CurrentFindOptions => new(MatchCase, WholeWords);

    private void ShowFindPanel(bool replace)
    {
        var wasOpen = IsFindPanelOpen;

        IsFindPanelOpen = true;

        if (!wasOpen)
        {
            PrefillSearchFromSelection();
            IsReplaceRowVisible = replace;
        }
        else if (replace)
        {
            IsReplaceRowVisible = true;
        }

        RefreshSearchResults(resetActive: false);
        FindFocusRequest++;
    }

    private void PrefillSearchFromSelection()
    {
        if (_history.Selection.IsCollapsed)
        {
            return;
        }

        var text = DocumentFragment.Capture(Document, _history.Selection).PlainText;

        if (text.Length is > 0 and <= 200
            && !text.Contains('\n')
            && !text.Contains('\f'))
        {
            FindQuery = text;
        }
    }

    private void RefreshSearchResults(bool resetActive)
    {
        if (!IsFindPanelOpen || FindQuery.Length == 0)
        {
            ClearSearchResults();
            return;
        }

        _searchMatches = SearchEngine.Find(Document, FindQuery, CurrentFindOptions);

        if (resetActive || _activeSearchMatchIndex >= _searchMatches.Length)
        {
            _activeSearchMatchIndex = -1;
        }

        if (resetActive)
        {
            _findNotice = string.Empty;
        }

        var highlights = new List<Selection>(_searchMatches.Length);

        foreach (var match in _searchMatches)
        {
            highlights.Add(SearchEngine.GetSelection(Document, match));
        }

        _searchHighlights = highlights;
        OnPropertyChanged(nameof(SearchHighlights));
        UpdateFindStatus();
    }

    private void ClearSearchResults()
    {
        _searchMatches = [];
        _searchHighlights = [];
        _activeSearchMatchIndex = -1;
        _findNotice = string.Empty;
        OnPropertyChanged(nameof(SearchHighlights));
        UpdateFindStatus();
    }

    private void UpdateFindStatus()
    {
        OnPropertyChanged(nameof(FindStatusText));
        OnPropertyChanged(nameof(HasSearchMatches));
        OnPropertyChanged(nameof(FindMatchCount));
    }

    private void MoveToMatch(int index, bool wrapped)
    {
        if (index < 0 || index >= _searchMatches.Length)
        {
            return;
        }

        _activeSearchMatchIndex = index;
        _findNotice = wrapped
            ? index == 0
                ? "Wrapped to the beginning."
                : "Wrapped to the end."
            : string.Empty;
        Selection = SearchEngine.GetSelection(Document, _searchMatches[index]);
        UpdateFindStatus();
    }

    private int FindForwardIndex(bool afterCaret = false)
    {
        var caret = _history.Selection.GetStart(Document);

        for (var index = 0; index < _searchMatches.Length; index++)
        {
            var start = SearchEngine
                .GetSelection(Document, _searchMatches[index])
                .GetStart(Document);
            var comparison = TextNavigation.Compare(Document, start, caret);

            if (afterCaret ? comparison > 0 : comparison >= 0)
            {
                return index;
            }
        }

        return -1;
    }

    private int FindBackwardIndex()
    {
        var caret = _history.Selection.GetStart(Document);

        for (var index = _searchMatches.Length - 1; index >= 0; index--)
        {
            var start = SearchEngine
                .GetSelection(Document, _searchMatches[index])
                .GetStart(Document);

            if (TextNavigation.Compare(Document, start, caret) < 0)
            {
                return index;
            }
        }

        return -1;
    }

    private void SelectMatchAfterCaret()
    {
        if (_searchMatches.Length == 0)
        {
            _activeSearchMatchIndex = -1;
            UpdateFindStatus();
            return;
        }

        var index = FindForwardIndex(afterCaret: true);

        if (index < 0)
        {
            _activeSearchMatchIndex = -1;
            UpdateFindStatus();
            return;
        }

        MoveToMatch(index, wrapped: false);
    }
}
