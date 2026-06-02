using System.Collections.ObjectModel;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
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

	private readonly PatternItemsFactory _patternItemsFactory;
	private readonly AutomationElement _rootElement;
	private readonly AutomationBase _automation;
	private readonly InternalLogger _logger;
	private readonly int _processId;
	private readonly ITreeWalker _treeWalker;
	private readonly IntPtr _windowHandle;
	private FocusTrackingMode? _focusTrackingMode;
	private ElementOverlay _trackHighlighterOverlay;
	private static readonly ElementPatternItem[] _defaultPatternItems = [
																		new ElementPatternItem("Identification", PatternItemsFactory.Identification, true, true),
																	new ElementPatternItem("Details", PatternItemsFactory.Details, true, true),
																	new ElementPatternItem("Pattern Support", PatternItemsFactory.PatternSupport, true, true)
																	];

	public ProcessViewModel(AutomationBase automation, int processId, IntPtr mainWindowHandle, InternalLogger logger, bool controlWalker = true) : this(automation, processId, mainWindowHandle, logger, controlWalker ? automation.TreeWalkerFactory.GetControlViewWalker() : automation.TreeWalkerFactory.GetRawViewWalker()) { }

	public ProcessViewModel(AutomationBase automation, int processId, IntPtr mainWindowHandle, InternalLogger logger, ITreeWalker treeWalker) {
		_logger = logger;
		_automation = automation;
		_processId = processId;
		_windowHandle = mainWindowHandle;
		_treeWalker = treeWalker;
		_rootElement = _windowHandle == 0
			? _automation.GetDesktop()
			: _automation.FromHandle(_windowHandle);
		_patternItemsFactory = new PatternItemsFactory(_automation);
		ElementPatterns = GetDefaultPatternList();

		_trackHighlighterOverlay = CreateTrackHighlighterOverlay();

		WindowTitle = $"Process: [{processId}] '{(processId != 0
			? _automation.FromHandle(mainWindowHandle)?.Properties.Name ?? "N/A"
			: "Desktop")}'";

		HoverManager.AddListener(_windowHandle,
								 x => {
									 if (EnableHoverMode)
										 _ = ElementToSelectChanged(x);
								 });
		HoverManager.Disable(_windowHandle);

		Elements = [];

		RefreshCommand = new AsyncRelayCommand(async _ => await Task.Run(Initialize));
		CaptureSelectedItemCommand = new RelayCommand(_ => {
			if (SelectedItem?.AutomationElement == null)
				return;

			var capturedImage = SelectedItem.AutomationElement.Capture();
			SaveFileDialog saveDialog = new() {
				Filter = "Png file (*.png)|*.png"
			};

			if (saveDialog.ShowDialog() == true)
				capturedImage.Save(saveDialog.FileName, ImageFormat.Png);

			capturedImage.Dispose();
		});

		CurrentElementSaveStateCommand = new RelayCommand(_ => {
			if (SelectedItem?.AutomationElement == null)
				return;

			try {

				Clipboard.SetText(new XmlTreeExporter(EnableXPath).Export(SelectedItem));
				CopiedNotificationCurrentElementSaveStateRequested?.Invoke();
			}
			catch (Exception e) {
				_logger?.LogError(e.ToString());
			}
		});

		ClosingCommand = new RelayCommand(_ => {
			HoverManager.RemoveListener(_windowHandle);
			_trackHighlighterOverlay?.Dispose();
			_focusTrackingMode?.Stop();
			_focusTrackingMode = null;
		});

		CopyDetailsToClipboardCommand = new RelayCommand(param => {
			if (SelectedItem?.AutomationElement == null)
				return;

			try {
				var depthMax = param switch {
					int i => i,
					string s when int.TryParse(s, out var i) => i,
					_ => 0 // valeur par défaut (clic normal sur le bouton)
				};
				Clipboard.SetText(new XmlElementDetailsExporter().Export(this, depthMax));

				CopiedNotificationRequested?.Invoke();
			}
			catch (Exception e) {
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

	public ObservableCollection<ElementPatternItem> ElementPatterns {
		get;
		private set => SetProperty(ref field, value);
	}

	public ElementViewModel? SelectedItem {
		get => GetProperty<ElementViewModel>();
		set {
			if (!SetProperty(value))
				return;

			if (EnableHighLightSelectionMode)
				TrackSelectedItem(value);

			_ = Task.Run(() => ReadPatternsForSelectedItem(value?.AutomationElement));
		}
	}

	public bool EnableHoverMode {
		get => GetProperty<bool>();
		set {
			_ = GetProperty<bool>() && value ? SetProperty(false) : SetProperty(value);
			SetMode();
		}
	}

	public bool EnableHighLightSelectionMode {
		get => GetProperty<bool>();
		set {
			_ = GetProperty<bool>() && value ? SetProperty(false) : SetProperty(value);
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
			_ = GetProperty<bool>() && value ? SetProperty(false) : SetProperty(value);
			SetMode();
		}
	}

	private static ElementOverlay CreateTrackHighlighterOverlay() => App.FlaUiAppOptions.SelectionOverlay() ?? App.FlaUiAppOptions.DefaultOverlay();

	private void TrackSelectedItem(ElementViewModel? item) {
		if (item is null || item.Level <= 0) {
			_trackHighlighterOverlay?.Dispose();
			return;
		}

		if (item.AutomationElement == null)
			return;

		_trackHighlighterOverlay?.Dispose();
		_trackHighlighterOverlay = CreateTrackHighlighterOverlay();

		try {
			_trackHighlighterOverlay.Show(item.AutomationElement.Properties.BoundingRectangle.Value);
		}
		catch (Exception) {
			_trackHighlighterOverlay?.Dispose();
		}
	}

	private void SetMode() {
		HoverManager.Disable(_windowHandle);
		_trackHighlighterOverlay?.Dispose();
		_focusTrackingMode?.Stop();

		if (EnableFocusTrackingMode)
			_focusTrackingMode?.Start();
		else if (EnableHighLightSelectionMode)
			TrackSelectedItem(SelectedItem);
		else if (EnableHoverMode)
			HoverManager.Enable(_windowHandle);
	}

	public event Action? CopiedNotificationCurrentElementSaveStateRequested;
	public event Action? CopiedNotificationRequested;

	public void Initialize() {
		ElementViewModel desktopViewModel = new(_rootElement, null, 0, _logger, 2);

		Elements = new ObservableCollection<ElementViewModel>(desktopViewModel.Children);

		// Initialize focus tracking
		_focusTrackingMode ??= new FocusTrackingMode(_automation,
													   x => EnableFocusTrackingMode ? ElementToSelectChanged(x)?.AutomationElement : null);
		SelectedItem = desktopViewModel;

		OnPropertyChanged(nameof(Elements));
		OnPropertyChanged(nameof(ElementPatterns));
	}

	public ElementViewModel? ElementToSelectChanged(AutomationElement? obj, bool forceExpand = false) => SelectedItem = GetNextElementVm(forceExpand, GetPathToRoot(obj, forceExpand), Elements);

	private Stack<AutomationElement> GetPathToRoot(AutomationElement? obj, bool forceExpand) {
		Stack<AutomationElement> pathToRoot = new();

		while (obj?.Properties.ProcessId.IsSupported == true && obj.Properties.ProcessId == _processId) {
			// Break on circular relationship (should not happen?)
			if (pathToRoot.Contains(obj) || obj.Equals(_rootElement))
				break;

			pathToRoot.Push(obj);

			if (forceExpand)
				break;

			try {
				obj = _treeWalker.GetParent(obj);
			}
			catch (Exception ex) {
				_logger?.LogError($"Exception: {ex.Message}");
			}
		}

		return pathToRoot;
	}

	private ElementViewModel? GetNextElementVm(bool forceExpand, Stack<AutomationElement> pathToRoot, IEnumerable<ElementViewModel> viewModels) {
		ElementViewModel? nextElementVm = null;

		while (pathToRoot.Count > 0) {
			var elementOnPath = pathToRoot.Pop();
			nextElementVm = FindElement(viewModels, elementOnPath);

			if (nextElementVm is null || (!forceExpand && nextElementVm.IsExpanded))
				continue;

			if (pathToRoot.Count != 0)
				nextElementVm.IsExpanded = true;

			ExpandElement(nextElementVm);

			if (forceExpand)
				break;
		}

		return nextElementVm;
	}

	private ElementViewModel? FindElement(IEnumerable<ElementViewModel> viewModels, AutomationElement element) => viewModels.FirstOrDefault(el => {
		if (el?.AutomationElement == null)
			return false;

		try {
			return el.AutomationElement.Equals(element);
		}
		catch (Exception e) {
			_logger?.LogError(e.ToString());
		}

		return false;
	});

	private void ReadPatternsForSelectedItem(AutomationElement? selectedItemAutomationElement) {
		try {
			_ = SetUpElementPatterns(selectedItemAutomationElement, ElementPatterns);
		}
		catch (Exception e) {
			_logger?.LogError(e.ToString());
		}
	}

	private ObservableCollection<ElementPatternItem> GetDefaultPatternList()
	=> [
		.. _defaultPatternItems,
		.. _automation.PatternLibrary.AllForCurrentFramework
			.Select(x => new ElementPatternItem(x.Name, x.Name, true)),
	];

	public ObservableCollection<ElementPatternItem>? GetElementPatterns(AutomationElement automationElement)
		=> SetUpElementPatterns(automationElement, GetDefaultPatternList());

	public ObservableCollection<ElementPatternItem>? SetUpElementPatterns(AutomationElement? selectedItemAutomationElement, ObservableCollection<ElementPatternItem> elementPatternsReference) {
		if (selectedItemAutomationElement is null || elementPatternsReference is null)
			return [];

		HashSet<PatternId> supportedPatterns = [.. selectedItemAutomationElement.GetSupportedPatterns()];
		var createdPattern = _patternItemsFactory.CreatePatternItemsForElement(selectedItemAutomationElement, supportedPatterns);

		foreach (var elementPattern in elementPatternsReference) {
			elementPattern.Children = createdPattern.TryGetValue(elementPattern.PatternIdName, out var children)
				? new ObservableCollection<PatternItem>(children)
				: [];

			elementPattern.IsVisible = elementPattern.Children.Any()
				&& (elementPattern.PatternIdName == PatternItemsFactory.Identification
					|| elementPattern.PatternIdName == PatternItemsFactory.Details
					|| elementPattern.PatternIdName == PatternItemsFactory.PatternSupport
					|| supportedPatterns.Any(x => x.Name.Equals(elementPattern.PatternIdName, StringComparison.Ordinal)));
		}

		return elementPatternsReference;
	}

	public void ExpandElement(ElementViewModel sender) => ExpandElement(sender, Elements);

	private static void ExpandElement(ElementViewModel sender, ObservableCollection<ElementViewModel> elements) {
		var senderIndex = elements.IndexOf(sender);
		if (senderIndex < 0)
			return;

		var children = sender.LoadChildren(1);

		foreach (var child in children)
			if (!elements.Contains(child))
				try {
					elements.Insert(++senderIndex, child);
				}
				catch (NotSupportedException) { }
				catch (InvalidOperationException) { }
	}

	public void CollapseElement(ElementViewModel sender) => CollapseElement(sender, Elements);

	private static void CollapseElement(ElementViewModel sender, ObservableCollection<ElementViewModel> elements) {
		var senderIndex = elements.IndexOf(sender);

		if (senderIndex < 0)
			return;

		var removeCount = 0;

		for (var i = senderIndex + 1; i < elements.Count; i++)
			if (IsDescendantOf(elements[i], sender))
				removeCount++;
			else
				break;

		for (var i = 0; i < removeCount; i++)
			elements.RemoveAt(senderIndex + 1);
	}

	private static bool IsDescendantOf(ElementViewModel? node, ElementViewModel? parent) {
		if (node is null || parent is null)
			return false;

		do
			node = node.Parent;
		while (node is not null && node != parent);

		return node is not null && node == parent;
	}
}