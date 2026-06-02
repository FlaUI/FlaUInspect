using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FlaUI.Core;
using FlaUI.UIA2;
using FlaUI.UIA3;
using FlaUInspect.Core;
using FlaUInspect.Core.Logger;
using FlaUInspect.ViewModels;
using Application = System.Windows.Application;

namespace FlaUInspect.Views;

public partial class StartupWindow {
	private readonly InternalLogger _logger;

	public StartupWindow(InternalLogger logger) {
		_logger = logger;
		InitializeComponent();
	}

	private async void Uia2Click(object sender, RoutedEventArgs e) => await OpenProcessWindow(new UIA2Automation(), ControlTreeViewWalkerCheckBox.IsChecked == true);

	private async void Uia3Click(object sender, RoutedEventArgs e) => await OpenProcessWindow(new UIA3Automation(), ControlTreeViewWalkerCheckBox.IsChecked == true);

	private async Task OpenProcessWindow(AutomationBase automationBase, bool controlWalker) {
		if (ProcessesListBox.SelectedItem is not ProcessWindowInfo processWindowInfo)
			return;

		HoverMouseInitialize();
		ProcessViewModel processViewModel = new(automationBase, processWindowInfo.ProcessId, processWindowInfo.MainWindowHandle, _logger, controlWalker);

		ProcessWindow processWindow = new() { DataContext = processViewModel };
		processWindow.Show();
		await Task.Run(processViewModel.Initialize);
		//WindowState = WindowState.Minimized;
		return;
	}

	private static void HoverMouseInitialize() {
		if (!HoverManager.IsInitialized)
			HoverManager.Initialize(new UIA3Automation(), () => App.FlaUiAppOptions.HoverOverlay());
	}

	private void CloseClick(object sender, RoutedEventArgs e) {
		if (Application.Current.Windows.Count >= 1)
			Close();
		else
			WindowState = WindowState.Minimized;
	}

	private void PickWindowButton_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
		if (sender is Button { Command: { } command } && command.CanExecute(null))
			command.Execute(null);
	}

	private async void ProcessesListBoxOnMouseDoubleClick(object sender, MouseButtonEventArgs e) {
		if (Uia2RadioButton.IsChecked == true)
			await OpenProcessWindow(new UIA2Automation(), ControlTreeViewWalkerCheckBox.IsChecked == true);
		else if (Uia3RadioButton.IsChecked == true)
			await OpenProcessWindow(new UIA3Automation(), ControlTreeViewWalkerCheckBox.IsChecked == true);
	}
}