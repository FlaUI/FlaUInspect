using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Input;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Identifiers;
using FlaUI.Core.Patterns;
using FlaUInspect.Controls;
using FlaUInspect.Core;
using FlaUInspect.Core.Exporters;
using FlaUInspect.Core.Logger;
using FlaUInspect.Models;
using Microsoft.Win32;
using Application = System.Windows.Application;

namespace FlaUInspect.ViewModels;

public class ProcessViewModel : ObservableObject {

    private readonly AutomationBase _automation;
    private readonly InternalLogger _logger;
    private readonly int _processId;
    private readonly ITreeWalker _treeWalker;
    private readonly IntPtr _windowHandle;
    private ObservableCollection<ElementPatternItem>? _elementPatterns;
    private FocusTrackingMode? _focusTrackingMode;
    private PatternItemsFactory? _patternItemsFactory;
    private AutomationElement? _rootElement;
    private ElementOverlay _trackHighlighterOverlay;

    // ── Search state ──────────────────────────────────────────────────────────
    private int _lastFoundLoadedIndex = -1;
    private AutomationElement? _lastFoundAutomationElement;
    private CancellationTokenSource? _searchCts;

    public bool IsSearching {
        get => GetProperty<bool>();
        private set => SetProperty(value);
    }

    public bool SearchNotFound {
        get => GetProperty<bool>();
        private set => SetProperty(value);
    }

    public ProcessViewModel(AutomationBase automation, int processId, IntPtr mainWindowHandle, InternalLogger logger) {
        _logger = logger;
        _automation = automation;
        _processId = processId;
        _windowHandle = mainWindowHandle;

        _trackHighlighterOverlay = CreateTrackHighlighterOverlay();

        WindowTitle = $"Process: [{processId}] '{(processId != 0
            ? _automation.FromHandle(mainWindowHandle)?.Properties.Name ?? "N/A"
            : "Desktop")}'";

        HoverManager.AddListener(_windowHandle,
                                 x => {
                                     if (EnableHoverMode) {
                                         ElementToSelectChanged(x);
                                     }
                                 });
        HoverManager.Disable(_windowHandle);

        _treeWalker = _automation.TreeWalkerFactory.GetControlViewWalker();

        Elements = [];

        RefreshCommand = new AsyncRelayCommand(async () => await Task.Run(Initialize));
        CaptureSelectedItemCommand = new RelayCommand(_ => {
            if (SelectedItem?.AutomationElement == null) {
                return;
            }
            Bitmap capturedImage = SelectedItem.AutomationElement.Capture();
            SaveFileDialog saveDialog = new () {
                Filter = "Png file (*.png)|*.png"
            };

            if (saveDialog.ShowDialog() == true) {
                capturedImage.Save(saveDialog.FileName, ImageFormat.Png);
            }
            capturedImage.Dispose();
        });

        CurrentElementSaveStateCommand = new RelayCommand(_ => {
            if (SelectedItem?.AutomationElement == null) {
                return;
            }

            try {
                ITreeExporter exporter = new XmlTreeExporter(EnableXPath);
                string exportedTree = exporter.Export(SelectedItem);

                Clipboard.SetText(exportedTree.ToString());
                CopiedNotificationCurrentElementSaveStateRequested?.Invoke();
            } catch (Exception e) {
                _logger?.LogError(e.ToString());
            }
        });

        ClosingCommand = new RelayCommand(_ => {
            HoverManager.RemoveListener(_windowHandle);
            _trackHighlighterOverlay?.Dispose();
            _focusTrackingMode?.Stop();
            _focusTrackingMode = null;
        });

        CopyDetailsToClipboardCommand = new RelayCommand(_ => {
            if (SelectedItem?.AutomationElement == null) {
                return;
            }

            try {
                IElementDetailsExporter detailsExporter = new XmlElementDetailsExporter();
                string details = detailsExporter.Export(ElementPatterns);

                Clipboard.SetText(details);
                CopiedNotificationRequested?.Invoke();
            } catch (Exception e) {
                _logger?.LogError(e.ToString());
            }
        });
    }

    public string? WindowTitle { get; }

    public bool EnableXPath {
        get => GetProperty<bool>();
        set => SetProperty(value);
    }

    public ObservableCollection<ElementViewModel> Elements { get; private set; }
    public ObservableCollection<ElementViewModel>? FlatNodes {
        get => GetProperty<ObservableCollection<ElementViewModel>>();
        private set => SetProperty(value);
    }

    public IEnumerable<ElementPatternItem> ElementPatterns {
        get => _elementPatterns ?? Enumerable.Empty<ElementPatternItem>();
        private set => SetProperty(ref _elementPatterns, value as ObservableCollection<ElementPatternItem>);
    }

    public ElementViewModel? SelectedItem {
        get => GetProperty<ElementViewModel>();
        set {
            if (SetProperty(value)) {
                if (value != null) {
                    if (EnableHighLightSelectionMode) {
                        TrackSelectedItem(value);
                    }
                    Task.Run(() => ReadPatternsForSelectedItem(value.AutomationElement));
                }
            }
        }
    }

    public bool EnableHoverMode {
        get => GetProperty<bool>();
        set {
            SetProperty(value);
            SetMode();
        }
    }

    public bool EnableHighLightSelectionMode {
        get => GetProperty<bool>();
        set {
            SetProperty(value);
            SetMode();
        }
    }

    public ICommand ClosingCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand CaptureSelectedItemCommand { get; }
    public ICommand CurrentElementSaveStateCommand { get; }
    public ICommand CopyDetailsToClipboardCommand { get; }

    public bool EnableFocusTrackingMode {
        get => GetProperty<bool>();
        set {
            SetProperty(value);
            SetMode();
        }
    }

    private static ElementOverlay CreateTrackHighlighterOverlay() {
        return App.FlaUiAppOptions.SelectionOverlay() ?? App.FlaUiAppOptions.DefaultOverlay()!;
    }

    private void TrackSelectedItem(ElementViewModel item) {
        if (item.AutomationElement != null) {
            _trackHighlighterOverlay?.Dispose();
            _trackHighlighterOverlay = CreateTrackHighlighterOverlay();

            try {
                _trackHighlighterOverlay.Show(item.AutomationElement.Properties.BoundingRectangle.Value);
            } catch (Exception e) {
                _trackHighlighterOverlay?.Dispose();
            }
        }
    }

    private void SetMode() {
        HoverManager.Disable(_windowHandle);
        _trackHighlighterOverlay?.Dispose();
        _focusTrackingMode?.Stop();

        if (new[] { EnableHoverMode, EnableHighLightSelectionMode, EnableFocusTrackingMode }.Count(x => x) == 1) {
            if (EnableFocusTrackingMode) {
                _focusTrackingMode?.Start();
            } else if (EnableHighLightSelectionMode) {
                if (SelectedItem != null) {
                    TrackSelectedItem(SelectedItem);
                }
            } else if (EnableHoverMode) {
                HoverManager.Enable(_windowHandle);
            }
        }
    }

    public event Action? CopiedNotificationCurrentElementSaveStateRequested;
    public event Action CopiedNotificationRequested;

    public void Initialize() {
        _patternItemsFactory = new PatternItemsFactory(_automation);

        _rootElement = _windowHandle == IntPtr.Zero
            ? _automation.GetDesktop()
            : _automation.FromHandle(_windowHandle);

        ElementViewModel desktopViewModel = new (_rootElement, null, 0, _logger);

        List<ElementViewModel> topChildren = desktopViewModel.LoadChildren();

        Elements = new ObservableCollection<ElementViewModel>(topChildren);

        // Initialize hover
        EnableHoverMode = false;

        // Initialize focus tracking
        _focusTrackingMode = new FocusTrackingMode(_automation,
                                                   x => {
                                                       if (EnableFocusTrackingMode) {
                                                           ElementToSelectChanged(x);
                                                       }
                                                   });

        ElementPatterns = GetDefaultPatternList();
        SelectedItem = Elements.Count == 0 ? null : Elements[0];

        OnPropertyChanged(nameof(Elements));
        OnPropertyChanged(nameof(ElementPatterns));
    }

    public void ElementToSelectChanged(AutomationElement? obj, bool forceExpand = false) {
        Stack<AutomationElement> pathToRoot = new ();

        while (obj != null && obj.Properties.ProcessId == _processId) {
            // Break on circular relationship (should not happen?)
            if (pathToRoot.Contains(obj) || obj.Equals(_rootElement)) {
                break;
            }

            pathToRoot.Push(obj);

            if (forceExpand) {
                break;
            }

            try {
                obj = _treeWalker.GetParent(obj);
            } catch (Exception ex) {
                _logger?.LogError($"Exception: {ex.Message}");
            }
        }

        IEnumerable<ElementViewModel> viewModels = Elements;
        ElementViewModel? nextElementVm = null;

        while (pathToRoot.Count > 0) {
            AutomationElement elementOnPath = pathToRoot.Pop();
            nextElementVm = FindElement(viewModels, elementOnPath);

            if (nextElementVm != null && (forceExpand || !nextElementVm.IsExpanded)) {
                if (pathToRoot.Count != 0) {
                    nextElementVm.IsExpanded = true;
                }
                ExpandElement(nextElementVm);

                if (forceExpand) {
                    break;
                }
            }
        }

        SelectedItem = nextElementVm;
    }

    private ElementViewModel? FindElement(IEnumerable<ElementViewModel> viewModels, AutomationElement element) {
        return viewModels.FirstOrDefault(el => {
            if (el?.AutomationElement == null) {
                return false;
            }

            try {
                return el.AutomationElement.Equals(element);
            } catch (Exception e) {
                _logger?.LogError(e.ToString());
            }

            return false;
        });
    }

    private ObservableCollection<ElementPatternItem> GetDefaultPatternList() {
        return new ObservableCollection<ElementPatternItem>(new[] {
                                                                    new ElementPatternItem("Identification", PatternItemsFactory.Identification, true, true),
                                                                    new ElementPatternItem("Details", PatternItemsFactory.Details, true, true),
                                                                    new ElementPatternItem("Pattern Support", PatternItemsFactory.PatternSupport, true, true)
                                                                }
                                                                .Concat(
                                                                        (_automation?.PatternLibrary.AllForCurrentFramework ?? [])
                                                                        .Select(x => {
                                                                            ElementPatternItem patternItem = new (x.Name, x.Name) {
                                                                                IsVisible = true
                                                                            };
                                                                            return patternItem;
                                                                        })));
    }

    private void ReadPatternsForSelectedItem(AutomationElement? selectedItemAutomationElement) {
        if (SelectedItem?.AutomationElement == null || selectedItemAutomationElement == null) {
            return;
        }

        if (_patternItemsFactory == null) {
            return;
        }

        try {
            HashSet<PatternId> supportedPatterns = [.. selectedItemAutomationElement.GetSupportedPatterns()];
            IDictionary<string, PatternItem[]> patternItemsForElement = _patternItemsFactory.CreatePatternItemsForElement(selectedItemAutomationElement, supportedPatterns);

            foreach (ElementPatternItem elementPattern in ElementPatterns) {
                elementPattern.IsVisible = elementPattern.PatternIdName == PatternItemsFactory.Identification
                                           || elementPattern.PatternIdName == PatternItemsFactory.Details
                                           || elementPattern.PatternIdName == PatternItemsFactory.PatternSupport
                                           || supportedPatterns.Any(x => x.Name.Equals(elementPattern.PatternIdName));


                elementPattern.Children = patternItemsForElement.TryGetValue(elementPattern.PatternIdName, out PatternItem[]? children)
                    ? new ObservableCollection<PatternItem>(children)
                    : [];

                if (!elementPattern.Children.Any()) {
                    elementPattern.IsVisible = false;
                }
            }
        } catch (Exception e) {
            _logger?.LogError(e.ToString());
        }
    }

    public void ExpandElement(ElementViewModel sender) {
        List<ElementViewModel> children = sender.LoadChildren();
        children.Reverse();

        int senderIndex = Elements.IndexOf(sender);

        if (senderIndex < 0) {
            return;
        }

        foreach (ElementViewModel child in children) {
            Elements.Insert(senderIndex + 1, child);
        }
    }

    public void CollapseElement(ElementViewModel sender) {
        int senderIndex = Elements.IndexOf(sender);

        if (senderIndex < 0) {
            return;
        }

        var removeCount = 0;

        for (int i = senderIndex + 1; i < Elements.Count; i++) {
            if (IsDescendantOf(Elements[i], sender)) {
                removeCount++;
            } else {
                break;
            }
        }

        for (var i = 0; i < removeCount; i++) {
            Elements.RemoveAt(senderIndex + 1);
        }
    }

    private bool IsDescendantOf(ElementViewModel? node, ElementViewModel? parent) {
        if (node == null || parent == null) {
            return false;
        }
        ElementViewModel? p = node.Parent;

        while (p != null) {
            if (p == parent)
                return true;
            p = p.Parent;
        }
        return false;
    }

    // ── Public search API (called from code-behind) ───────────────────────────

    /// <summary>Resets the search cursor. Call when criteria changes.</summary>
    public void ResetSearch() {
        _lastFoundLoadedIndex = -1;
        _lastFoundAutomationElement = null;
        _searchCts?.Cancel();
        _searchCts = null;
        IsSearching = false;
        SearchNotFound = false;
    }

    public void FindNext(FindCriteria criteria) {
        if (criteria.IsEmpty) return;
        SearchNotFound = false;

        if (criteria.SearchInLoadedOnly) {
            FindInLoaded(criteria, true);
        } else {
            if (IsSearching) return;
            StartFullTreeSearch(criteria, true);
        }
    }

    public void FindPrev(FindCriteria criteria) {
        if (criteria.IsEmpty) return;
        SearchNotFound = false;

        if (criteria.SearchInLoadedOnly) {
            FindInLoaded(criteria, false);
        } else {
            if (IsSearching) return;
            StartFullTreeSearch(criteria, false);
        }
    }

    // ── Loaded-tree search ────────────────────────────────────────────────────

    private void FindInLoaded(FindCriteria criteria, bool forward) {
        // Build the pool – all visible elements or children of SelectedItem only
        var pool = (criteria.SearchInChildrenOnly && SelectedItem != null
            ? Elements.Where(e => IsDescendantOf(e, SelectedItem))
            : (IEnumerable<ElementViewModel>)Elements).ToList();

        if (pool.Count == 0) return;

        // Determine start position based on last found or current selection
        int startPos = GetLoadedStartPos(pool, forward);

        for (var i = 0; i < pool.Count; i++) {
            int idx = forward
                ? (startPos + i) % pool.Count
                : ((startPos - i) % pool.Count + pool.Count) % pool.Count;

            if (MatchesViewModel(pool[idx], criteria)) {
                SelectedItem = pool[idx];
                _lastFoundLoadedIndex = Elements.IndexOf(pool[idx]);
                return;
            }
        }

        SearchNotFound = true;
    }

    private int GetLoadedStartPos(List<ElementViewModel> pool, bool forward) {
        // If we have a last-found element, start right after (or before) it
        if (_lastFoundLoadedIndex >= 0) {
            ElementViewModel? last = _lastFoundLoadedIndex < Elements.Count
                ? Elements[_lastFoundLoadedIndex]
                : null;

            if (last != null) {
                int poolIdx = pool.IndexOf(last);
                if (poolIdx >= 0)
                    return forward
                        ? (poolIdx + 1) % pool.Count
                        : (poolIdx - 1 + pool.Count) % pool.Count;
            }
        }

        // First search: start from right after SelectedItem (or 0)
        if (SelectedItem != null) {
            int poolIdx = pool.IndexOf(SelectedItem);
            if (poolIdx >= 0)
                return forward ? (poolIdx + 1) % pool.Count : poolIdx;
        }

        return forward ? 0 : pool.Count - 1;
    }

    // ── Full-tree DFS search ──────────────────────────────────────────────────

    private void StartFullTreeSearch(FindCriteria criteria, bool forward) {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        CancellationToken token = _searchCts.Token;

        // Seed _lastFoundAutomationElement from SelectedItem on fresh search
        if (_lastFoundAutomationElement == null && SelectedItem?.AutomationElement != null)
            _lastFoundAutomationElement = SelectedItem.AutomationElement;

        AutomationElement? searchRoot = criteria.SearchInChildrenOnly && SelectedItem?.AutomationElement != null
            ? SelectedItem.AutomationElement
            : _rootElement;

        IsSearching = true;

        Task.Run(async () => {
                     if (searchRoot == null) {
                         await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => IsSearching = false);
                         return;
                     }

                     try {
                         AutomationElement? found = forward
                             ? FindNextInTree(searchRoot, criteria, token)
                             : FindPrevInTree(searchRoot, criteria, token);

                         if (found == null || token.IsCancellationRequested) {
                             if (found == null && !token.IsCancellationRequested)
                                 await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => SearchNotFound = true);
                             return;
                         }

                         _lastFoundAutomationElement = found;
                         await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => ElementToSelectChanged(found));
                     } catch (OperationCanceledException) {
                         // expected on cancel – IsSearching already set to false by ResetSearch
                     } catch (Exception ex) {
                         _logger?.LogError(ex.ToString());
                     } finally {
                         if (!token.IsCancellationRequested) {
                             await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => IsSearching = false);
                         }
                     }
                 },
                 token);
    }

    /// <summary>DFS: finds the first element that matches criteria AFTER _lastFoundAutomationElement.</summary>
    private AutomationElement? FindNextInTree(AutomationElement root, FindCriteria criteria, CancellationToken ct) {
        bool pastLast = _lastFoundAutomationElement == null;
        (AutomationElement? found, _) = DfsForward(root, criteria, pastLast, ct);

        if (found == null && _lastFoundAutomationElement != null) {
            // Wrap around: search from root beginning
            (AutomationElement? wrapped, _) = DfsForward(root, criteria, true, ct);
            return wrapped;
        }
        return found;
    }

    /// <summary>Collect all DFS nodes, walk backwards to find prev match.</summary>
    private AutomationElement? FindPrevInTree(AutomationElement root, FindCriteria criteria, CancellationToken ct) {
        // Collect all matching elements in DFS order, pick the one before last found
        List<AutomationElement> matches = [];
        CollectMatches(root, criteria, matches, ct);

        if (matches.Count == 0) return null;

        if (_lastFoundAutomationElement == null)
            return matches[^1];

        for (var i = 0; i < matches.Count; i++) {
            bool isLast;

            try {
                isLast = matches[i].Equals(_lastFoundAutomationElement);
            } catch {
                isLast = false;
            }
            if (isLast)
                return matches[(i - 1 + matches.Count) % matches.Count];
        }

        // last found not in matches – return last match
        return matches[^1];
    }

    private void CollectMatches(AutomationElement node, FindCriteria criteria,
                                List<AutomationElement> result, CancellationToken ct) {
        ct.ThrowIfCancellationRequested();
        if (MatchesAutomationElement(node, criteria)) result.Add(node);

        AutomationElement[] children;

        try {
            using (CacheRequest.ForceNoCache()) {
                children = node.FindAllChildren();
            }
        } catch {
            return;
        }

        foreach (AutomationElement child in children) {
            ct.ThrowIfCancellationRequested();
            CollectMatches(child, criteria, result, ct);
        }
    }

    private (AutomationElement? found, bool pastLast) DfsForward(
        AutomationElement node, FindCriteria criteria, bool pastLast, CancellationToken ct) {

        ct.ThrowIfCancellationRequested();

        if (pastLast) {
            if (MatchesAutomationElement(node, criteria)) return (node, true);
        } else {
            try {
                if (node.Equals(_lastFoundAutomationElement)) pastLast = true;
            } catch {
            }
        }

        AutomationElement[] children;

        try {
            using (CacheRequest.ForceNoCache()) {
                children = node.FindAllChildren();
            }
        } catch {
            return (null, pastLast);
        }

        foreach (AutomationElement child in children) {
            ct.ThrowIfCancellationRequested();
            (AutomationElement? found, bool newPast) = DfsForward(child, criteria, pastLast, ct);
            pastLast = newPast;
            if (found != null) return (found, true);
        }

        return (null, pastLast);
    }

    // ── Match helpers ─────────────────────────────────────────────────────────

    private bool MatchesViewModel(ElementViewModel vm, FindCriteria criteria) {
        try {
            return criteria.FindBy switch {
                "Name" => MatchString(vm.Name, criteria),
                "AutomationId" => MatchString(vm.AutomationId, criteria),
                "ControlType" => criteria.ControlTypeValue.HasValue &&
                                 vm.ControlType == criteria.ControlTypeValue.Value,
                "ClassName" => MatchString(
                                           vm.AutomationElement?.Properties.ClassName.ValueOrDefault,
                                           criteria),
                "FrameworkId" => MatchString(
                                             vm.AutomationElement?.Properties.FrameworkId.ValueOrDefault,
                                             criteria),
                "FrameworkType" => criteria.FrameworkTypeValue.HasValue &&
                                   vm.AutomationElement != null &&
                                   vm.AutomationElement.Properties.FrameworkId
                                       .TryGetValue(out string? fid) &&
                                   string.Equals(fid,
                                                 criteria.FrameworkTypeValue.Value.ToString(),
                                                 StringComparison.OrdinalIgnoreCase),
                "ProcessId" => int.TryParse(criteria.TextValue, out int pid) &&
                               vm.AutomationElement != null &&
                               vm.AutomationElement.Properties.ProcessId
                                   .TryGetValue(out int epid) &&
                               epid == pid,
                "LocalizedControlType" => MatchString(
                                                      vm.AutomationElement?.Properties.LocalizedControlType.ValueOrDefault,
                                                      criteria),
                "HelpText" => MatchString(
                                          vm.AutomationElement?.Properties.HelpText.ValueOrDefault,
                                          criteria),
                "Text" or "Value" => vm.AutomationElement != null &&
                                     TryMatchValuePattern(vm.AutomationElement, criteria),
                _ => false
            };
        } catch {
            return false;
        }
    }

    private bool MatchesAutomationElement(AutomationElement el, FindCriteria criteria) {
        try {
            return criteria.FindBy switch {
                "Name" => MatchString(el.Properties.Name.ValueOrDefault, criteria),
                "AutomationId" => MatchString(el.Properties.AutomationId.ValueOrDefault, criteria),
                "ControlType" => criteria.ControlTypeValue.HasValue &&
                                 el.Properties.ControlType.TryGetValue(out ControlType ct) &&
                                 ct == criteria.ControlTypeValue.Value,
                "ClassName" => MatchString(el.Properties.ClassName.ValueOrDefault, criteria),
                "FrameworkId" => MatchString(el.Properties.FrameworkId.ValueOrDefault, criteria),
                "FrameworkType" => criteria.FrameworkTypeValue.HasValue &&
                                   el.Properties.FrameworkId.TryGetValue(out string? fid) &&
                                   string.Equals(fid,
                                                 criteria.FrameworkTypeValue.Value.ToString(),
                                                 StringComparison.OrdinalIgnoreCase),
                "ProcessId" => int.TryParse(criteria.TextValue, out int pid) &&
                               el.Properties.ProcessId.TryGetValue(out int epid) &&
                               epid == pid,
                "LocalizedControlType" => MatchString(
                                                      el.Properties.LocalizedControlType.ValueOrDefault,
                                                      criteria),
                "HelpText" => MatchString(el.Properties.HelpText.ValueOrDefault, criteria),
                "Text" => MatchString(el.Properties.Name, criteria),
                "Value" => TryMatchValuePattern(el, criteria),
                _ => false
            };
        } catch {
            return false;
        }
    }

    private bool TryMatchValuePattern(AutomationElement el, FindCriteria criteria) {
        try {
            if (el.Patterns.Value.TryGetPattern(out IValuePattern? vp))
                return MatchString(vp.Value.ValueOrDefault, criteria);
        } catch {
        }
        return false;
    }

    private bool MatchString(string? value, FindCriteria criteria) {
        if (value == null) return false;
        string pattern = criteria.TextValue ?? "";
        return criteria.MatchMode switch {
            SearchMatchMode.Exact      => string.Equals(value, pattern, StringComparison.Ordinal),
            SearchMatchMode.IgnoreCase => value.Contains(pattern, StringComparison.OrdinalIgnoreCase),
            _                          => value.Contains(pattern, StringComparison.Ordinal)   // Substring
        };
    }
}
