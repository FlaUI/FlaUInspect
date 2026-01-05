using System.Windows.Input;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUInspect.Core;

namespace FlaUInspect.ViewModels;

public class SearchViewModel : ObservableObject {
    private readonly Func<ElementViewModel?> _getSelectedItem;
    private readonly Func<ElementViewModel?> _getFirstElement;
    private readonly Func<ITreeWalker?> _getTreeWalker;
    private readonly Action<AutomationElement> _navigateToElement;

    private RelayCommand? _findNextCommand;
    private RelayCommand? _findPreviousCommand;
    private ElementViewModel? _searchStartNode;
    private IEnumerator<ElementViewModel>? _searchEnumerator;
    private readonly List<ElementViewModel> _searchHistory = [];
    private int _searchHistoryIndex = -1;
    private string _lastSearchText = string.Empty;
    private FindByType _lastFindByType;
    private bool _searchExhausted;
    private bool _userChangedSelection;

    public SearchViewModel(
        Func<ElementViewModel?> getSelectedItem,
        Func<ElementViewModel?> getFirstElement,
        Func<ITreeWalker?> getTreeWalker,
        Action<AutomationElement> navigateToElement) {
        _getSelectedItem = getSelectedItem;
        _getFirstElement = getFirstElement;
        _getTreeWalker = getTreeWalker;
        _navigateToElement = navigateToElement;
    }

    public bool IsNavigating { get; private set; }

    public FindByType SelectedFindByType {
        get => GetProperty<FindByType>();
        set => SetProperty(value);
    }

    public string SearchText {
        get => GetProperty<string>() ?? string.Empty;
        set {
            if (SetProperty(value)) {
                ResetSearchState();
            }
        }
    }

    public string PositionText {
        get => GetProperty<string>() ?? string.Empty;
        private set => SetProperty(value);
    }

    public ICommand FindNextCommand => _findNextCommand ??= new RelayCommand(_ => FindNext(), _ => !string.IsNullOrWhiteSpace(SearchText));

    public ICommand FindPreviousCommand => _findPreviousCommand ??= new RelayCommand(_ => FindPrevious(), _ => _searchHistoryIndex > 0);

    public void NotifySelectionChanged() {
        _userChangedSelection = true;
    }

    private void UpdatePositionText() {
        if (_searchHistory.Count == 0) {
            PositionText = _searchExhausted ? "0/0" : string.Empty;
        } else {
            var total = _searchExhausted ? _searchHistory.Count.ToString() : "?";
            PositionText = $"{_searchHistoryIndex + 1}/{total}";
        }
    }

    private void ResetSearchState() {
        _searchEnumerator?.Dispose();
        _searchEnumerator = null;
        _searchHistory.Clear();
        _searchHistoryIndex = -1;
        _searchStartNode = null;
        _searchExhausted = false;
        UpdatePositionText();
    }

    private void FindNext() {
        if (string.IsNullOrWhiteSpace(SearchText)) return;

        var selectedItem = _getSelectedItem();

        if (_searchHistoryIndex < _searchHistory.Count - 1) {
            _searchHistoryIndex++;
            NavigateToSearchResult(_searchHistory[_searchHistoryIndex]);
            UpdatePositionText();
            return;
        }

        var shouldStartNewSearch = _searchEnumerator == null
            || _lastSearchText != SearchText
            || _lastFindByType != SelectedFindByType
            || (_userChangedSelection && !IsDescendantOfSearchStart(selectedItem));

        _userChangedSelection = false;

        if (shouldStartNewSearch) {
            _searchStartNode = selectedItem ?? _getFirstElement();
            if (_searchStartNode == null) return;

            _lastSearchText = SearchText;
            _lastFindByType = SelectedFindByType;
            _searchHistory.Clear();
            _searchHistoryIndex = -1;
            _searchExhausted = false;
            _searchEnumerator?.Dispose();
            _searchEnumerator = EnumerateMatchingElements(_searchStartNode, SearchText, SelectedFindByType).GetEnumerator();
        }

        if (_searchEnumerator!.MoveNext()) {
            var found = _searchEnumerator.Current;
            _searchHistory.Add(found);
            _searchHistoryIndex = _searchHistory.Count - 1;
            NavigateToSearchResult(found);
        } else {
            _searchExhausted = true;
        }
        UpdatePositionText();
    }

    private bool IsDescendantOfSearchStart(ElementViewModel? node) {
        if (_searchStartNode == null || node == null) return false;
        if (node == _searchStartNode) return true;

        var current = node.AutomationElement;
        var target = _searchStartNode.AutomationElement;
        if (current == null || target == null) return false;

        var treeWalker = _getTreeWalker();
        if (treeWalker == null) return false;

        try {
            var parent = treeWalker.GetParent(current);
            while (parent != null) {
                if (parent.Equals(target)) return true;
                parent = treeWalker.GetParent(parent);
            }
        } catch {
            return false;
        }
        return false;
    }

    private void FindPrevious() {
        if (_searchHistoryIndex > 0) {
            _searchHistoryIndex--;
            NavigateToSearchResult(_searchHistory[_searchHistoryIndex]);
            UpdatePositionText();
        }
    }

    private void NavigateToSearchResult(ElementViewModel element) {
        if (element.AutomationElement == null) return;
        IsNavigating = true;
        try {
            _navigateToElement(element.AutomationElement);
        } finally {
            IsNavigating = false;
        }
    }

    private static IEnumerable<ElementViewModel> EnumerateMatchingElements(ElementViewModel startVm, string searchText, FindByType findBy) {
        if (startVm.AutomationElement == null) yield break;

        var stack = new Stack<(ElementViewModel vm, bool skipCheck)>();
        stack.Push((startVm, true));

        while (stack.Count > 0) {
            var (current, skipCheck) = stack.Pop();
            if (current.AutomationElement == null) continue;

            if (!skipCheck && MatchesCondition(current.AutomationElement, searchText, findBy)) {
                yield return current;
            }

            if (!current.IsExpanded) {
                current.IsExpanded = true;
            }

            for (int i = current.Children.Count - 1; i >= 0; i--) {
                var child = current.Children[i];
                if (child != null) {
                    stack.Push((child, false));
                }
            }
        }
    }

    private static bool MatchesCondition(AutomationElement element, string searchText, FindByType findBy) {
        try {
            return findBy switch {
                FindByType.FindFirstByXPath => element.FindFirstByXPath(searchText) != null,
                FindByType.ByText => (element.Properties.Name.ValueOrDefault?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
                                     || (element.AsTextBox()?.Text?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false),
                FindByType.ByFrameworkId => element.Properties.FrameworkId.ValueOrDefault?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false,
                FindByType.ByLocalizedControlType => element.Properties.LocalizedControlType.ValueOrDefault?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false,
                FindByType.ByName => element.Properties.Name.ValueOrDefault?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false,
                FindByType.ByAutomationId => element.Properties.AutomationId.ValueOrDefault?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false,
                FindByType.ByValue => GetValueFromElement(element)?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false,
                FindByType.ByControlType => element.Properties.ControlType.ValueOrDefault.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase),
                FindByType.ByClassName => element.Properties.ClassName.ValueOrDefault?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false,
                _ => false
            };
        } catch {
            return false;
        }
    }

    private static string? GetValueFromElement(AutomationElement element) {
        if (element.Patterns.Value.IsSupported) {
            return element.Patterns.Value.Pattern.Value.ValueOrDefault;
        }
        return element.AsTextBox()?.Text;
    }
}
