using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Input;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Identifiers;
using FlaUInspect.Core;
using FlaUInspect.Core.Exporters;
using FlaUInspect.Core.Logger;
using FlaUInspect.Models;
using Microsoft.Win32;

namespace FlaUInspect.ViewModels;

public class ProcessViewModel : ObservableObject {

    private readonly AutomationBase _automation;
    private readonly InternalLogger _logger;
    private readonly int _processId;
    private readonly ITreeWalker _treeWalker;
    private readonly ITreeWalker _rawTreeWalker;
    private readonly IntPtr _windowHandle;
    private ObservableCollection<ElementPatternItem>? _elementPatterns;
    private FocusTrackingMode? _focusTrackingMode;
    private PatternItemsFactory? _patternItemsFactory;
    private AutomationElement? _rootElement;
    private ElementOverlay _trackHighlighterOverlay;
    private ElementViewModel? _temporaryHoverNode;

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
        _rawTreeWalker = _automation.TreeWalkerFactory.GetRawViewWalker();

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
        _temporaryHoverNode = null;

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
        if (!forceExpand) {
            // Hover/focus tracking: expand the real parent chain from the top-level window
            // down to the target element.
            ExpandFullPathToSelect(obj);
            return;
        }

        // === forceExpand=true: original manual-expand logic, unchanged ===
        Stack<AutomationElement> pathToRoot = new ();

        AutomationElement? current = obj;
        while (current != null && current.Properties.ProcessId == _processId) {
            // Break on circular relationship (should not happen?)
            if (pathToRoot.Any(x => IsSameElement(x, current)) || IsSameElement(current, _rootElement)) {
                break;
            }

            pathToRoot.Push(current);

            if (forceExpand) {
                break;
            }

            try {
                current = _treeWalker.GetParent(current);
            } catch (Exception ex) {
                _logger?.LogError($"Exception: {ex.Message}");
                break;
            }
        }

        ElementViewModel? nextElementVm = null;

        // Walk the parent chain shallow-to-deep, locating and expanding each layer in the tree
        while (pathToRoot.Count > 0) {
            AutomationElement elementOnPath = pathToRoot.Pop();
            ElementViewModel? found = FindElement(Elements, elementOnPath);

            if (found == null) {
                // This layer is not visible in the tree (not expanded yet or dynamically rendered);
                // stop matching further.
                break;
            }

            nextElementVm = found;

            if (forceExpand || !found.IsExpanded) {
                if (pathToRoot.Count != 0) {
                    found.IsExpanded = true;
                }
                ExpandElement(found);
            }

            if (forceExpand) {
                break;
            }
        }

        // When the hovered element cannot be matched exactly in the tree (common for dynamically
        // rendered elements in Flutter/self-drawn apps), add it to the tree and select it so the
        // element and details panels update accordingly.
        if (obj != null && obj.Properties.ProcessId == _processId &&
            (nextElementVm == null || !IsSameElement(obj, nextElementVm.AutomationElement))) {
            nextElementVm = AddDynamicHoverNode(obj);
        } else if (_temporaryHoverNode != null && nextElementVm != null) {
            Elements.Remove(_temporaryHoverNode);
            _temporaryHoverNode = null;
        }

        SelectedItem = nextElementVm;
    }

    /// <summary>
    /// For the forceExpand=false path: walk the real parent chain with the TreeWalker from the
    /// top-level window, expanding layer by layer to the target element. Already-expanded nodes
    /// are skipped; when an intermediate layer is missing it is inserted via the TreeWalker
    /// instead of degrading to adding an orphaned ListItem at the tree top level.
    /// </summary>
    private void ExpandFullPathToSelect(AutomationElement? obj) {
        Stack<AutomationElement> pathToRoot = new ();

        // Use the RawView walker for the parent chain, matching the Elements collection
        // (FindAllChildren/RawView). ControlView misses anonymous Groups in Flutter/self-drawn
        // apps and produces incorrect parent-child relationships.
        AutomationElement? current = obj;
        const int maxPathDepth = 100;
        while (current != null && pathToRoot.Count < maxPathDepth) {
            if (IsSameElement(current, _rootElement)) {
                break;
            }
            // Circular detection: use IsSameElement only when both RuntimeIds are present
            // (reliable). When either side is null (Flutter/self-drawn apps) IsSameElement is
            // unreliable, so skip the check and rely on the genuine RawView parent chain plus
            // maxPathDepth as a safety limit.
            if (pathToRoot.Any(x => IsSameElementSafe(x, current))) {
                break;
            }

            pathToRoot.Push(current);

            try {
                current = _rawTreeWalker.GetParent(current);
            } catch (Exception ex) {
                _logger?.LogError($"Exception: {ex.Message}");
                break;
            }
        }

        ElementViewModel? nextElementVm = null;
        ElementViewModel? parentVm = null;

        // Walk the parent chain shallow-to-deep, locating and expanding each layer in the tree
        while (pathToRoot.Count > 0) {
            AutomationElement elementOnPath = pathToRoot.Pop();
            ElementViewModel? found = FindElementInScope(parentVm, elementOnPath);

            // When this layer is not visible in the tree, enumerate the parent's real children
            // with the TreeWalker to insert it.
            if (found == null) {
                found = InsertMissingElement(parentVm, elementOnPath);
            }

            if (found == null) {
                break;
            }

            nextElementVm = found;

            bool isLast = pathToRoot.Count == 0;
            // Skip already-expanded nodes to avoid reloading children; only non-leaf layers
            // need expanding.
            if (!isLast && !found.IsExpanded) {
                found.IsExpanded = true;
                ExpandElement(found);
            }

            parentVm = found;
        }

        // Fallback: when the hovered element cannot be matched exactly in the tree, add it
        // dynamically as a last resort.
        if (obj != null && obj.Properties.ProcessId == _processId &&
            (nextElementVm == null || !IsSameElement(obj, nextElementVm.AutomationElement))) {
            nextElementVm = AddDynamicHoverNode(obj);
        } else if (_temporaryHoverNode != null && nextElementVm != null) {
            Elements.Remove(_temporaryHoverNode);
            _temporaryHoverNode = null;
        }

        SelectedItem = nextElementVm;
    }

    /// <summary>
    /// For circular detection only: use IsSameElement (authoritative) only when both RuntimeIds
    /// are present; when either side is null return false (treated as different elements) to
    /// prevent SameIdentity from misjudging a cycle on same-named anonymous elements in
    /// Flutter/self-drawn apps.
    /// </summary>
    private static bool IsSameElementSafe(AutomationElement? a, AutomationElement? b) {
        if (a == null || b == null) {
            return false;
        }
        int[]? aRid = null;
        int[]? bRid = null;
        try {
            aRid = a.Properties.RuntimeId.ValueOrDefault;
            bRid = b.Properties.RuntimeId.ValueOrDefault;
        } catch {
        }
        if (aRid == null || bRid == null) {
            return false;
        }
        return IsSameElement(a, b);
    }

    private ElementViewModel? AddDynamicHoverNode(AutomationElement element) {
        if (_temporaryHoverNode != null) {
            Elements.Remove(_temporaryHoverNode);
            _temporaryHoverNode = null;
        }

        ElementViewModel viewModel = new (element, null, 0, _logger);
        _temporaryHoverNode = viewModel;
        Elements.Add(viewModel);
        return viewModel;
    }

    private ElementViewModel? FindElement(IEnumerable<ElementViewModel> viewModels, AutomationElement element) {
        foreach (ElementViewModel? el in viewModels) {
            if (el?.AutomationElement == null) {
                continue;
            }

            try {
                if (IsSameElement(el.AutomationElement, element)) {
                    return el;
                }
            } catch (Exception e) {
                _logger?.LogError(e.ToString());
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the range of parent's direct children (Level == parent.Level + 1) in the flat
    /// Elements list. The flat list is pre-order: the parent is immediately followed by its
    /// descendant range, ending at the first node with Level &lt;= parent.Level.
    /// </summary>
    private List<ElementViewModel> GetChildrenRange(ElementViewModel parent) {
        List<ElementViewModel> children = new();
        int idx = Elements.IndexOf(parent);
        if (idx < 0) {
            return children;
        }

        int childLevel = parent.Level + 1;
        for (int i = idx + 1; i < Elements.Count && Elements[i].Level > parent.Level; i++) {
            if (Elements[i].Level == childLevel) {
                children.Add(Elements[i]);
            }
        }

        return children;
    }

    /// <summary>
    /// Finds element within parentVm's direct children; when parentVm is null the root is not in
    /// the tree, so fall back to a global search of the top-level Elements (the top-level set is
    /// small, acceptable).
    /// </summary>
    private ElementViewModel? FindElementInScope(ElementViewModel? parentVm, AutomationElement element) {
        IEnumerable<ElementViewModel> scope = parentVm == null ? Elements : GetChildrenRange(parentVm);
        return FindElement(scope, element);
    }

    /// <summary>
    /// Returns the index of parent's last descendant in Elements; when it has no descendants
    /// returns parent's own index. Used to compute the insert position for InsertMissingElement,
    /// keeping the flat-list pre-order invariant.
    /// </summary>
    private int GetLastDescendantIndex(ElementViewModel parent) {
        int idx = Elements.IndexOf(parent);
        if (idx < 0) {
            return -1;
        }

        int last = idx;
        for (int i = idx + 1; i < Elements.Count && Elements[i].Level > parent.Level; i++) {
            last = i;
        }

        return last;
    }

    /// <summary>
    /// When a path element is not found in the tree, enumerate the parent's real children with
    /// the TreeWalker (RawView) to fill it in, create an ElementViewModel and insert it at the
    /// end of the parent's child range, keeping the pre-order invariant.
    /// </summary>
    private ElementViewModel? InsertMissingElement(ElementViewModel? parentVm, AutomationElement element) {
        AutomationElement? parentEl = parentVm?.AutomationElement ?? _rootElement;
        if (parentEl == null) {
            return null;
        }

        int level = (parentVm?.Level ?? 0) + 1;

        AutomationElement? child;
        try {
            child = _rawTreeWalker.GetFirstChild(parentEl);
        } catch (Exception ex) {
            _logger?.LogError($"Exception: {ex.Message}");
            return null;
        }

        while (child != null) {
            AutomationElement currentChild = child;
            if (IsSameElement(currentChild, element)) {
                ElementViewModel vm = new(currentChild, parentVm, level, _logger);
                if (parentVm == null) {
                    Elements.Add(vm);
                } else {
                    Elements.Insert(GetLastDescendantIndex(parentVm) + 1, vm);
                }
                return vm;
            }

            try {
                child = _rawTreeWalker.GetNextSibling(currentChild);
            } catch (Exception ex) {
                _logger?.LogError($"Exception: {ex.Message}");
                break;
            }
        }

        return null;
    }

    private static bool IsSameElement(AutomationElement? a, AutomationElement? b) {
        if (ReferenceEquals(a, b)) {
            return true;
        }

        if (a == null || b == null) {
            return false;
        }

        // RuntimeId is the stable element identifier provided by UIA. Prefer it for authoritative
        // comparison to avoid SameIdentity misjudging elements with the same name/type and an
        // empty BoundingRectangle (multiple anonymous Groups under the WeChat main window are a
        // typical case).
        int[]? aRuntimeId = null;
        int[]? bRuntimeId = null;
        try {
            aRuntimeId = a.Properties.RuntimeId.ValueOrDefault;
            bRuntimeId = b.Properties.RuntimeId.ValueOrDefault;
        } catch {
            // fall through to the degradation path below
        }

        // When both RuntimeIds are present, compare them byte-wise as the authoritative identity.
        if (aRuntimeId != null && bRuntimeId != null) {
            if (aRuntimeId.Length != bRuntimeId.Length) {
                return false;
            }
            for (int i = 0; i < aRuntimeId.Length; i++) {
                if (aRuntimeId[i] != bRuntimeId[i]) {
                    return false;
                }
            }
            return true;
        }

        // When RuntimeId is missing, fall back to FlaUI Equals; however it misjudges elements
        // with both RuntimeIds null as equal, so re-verify with SameIdentity (common in
        // Flutter/self-drawn apps).
        try {
            if (a.Equals(b)) {
                if (aRuntimeId == null && bRuntimeId == null) {
                    return SameIdentity(a, b);
                }
                return true;
            }
        } catch {
            // fall through to SameIdentity
        }

        // When Equals throws or both RuntimeIds are missing, finally fall back to
        // location + name + type matching.
        return SameIdentity(a, b);
    }

    private static bool SameIdentity(AutomationElement a, AutomationElement b) {
        try {
            if (a.Properties.ControlType.ValueOrDefault != b.Properties.ControlType.ValueOrDefault) {
                return false;
            }

            if ((a.Properties.Name.ValueOrDefault ?? string.Empty) != (b.Properties.Name.ValueOrDefault ?? string.Empty)) {
                return false;
            }

            Rectangle aRect = a.Properties.BoundingRectangle.ValueOrDefault;
            Rectangle bRect = b.Properties.BoundingRectangle.ValueOrDefault;

            if (aRect.IsEmpty || bRect.IsEmpty) {
                // When location cannot be compared, fall back to name + type matching
                return true;
            }

            return aRect == bRect;
        } catch {
            return false;
        }
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
}
