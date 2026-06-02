using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using FlaUInspect.ViewModels;
using Button = System.Windows.Controls.Button;
using ToggleButton = System.Windows.Controls.Primitives.ToggleButton;
using Window = System.Windows.Window;

namespace FlaUInspect.Views;

public partial class ProcessWindow : Window {
	public ProcessWindow() {
		InitializeComponent();
		Closed += ProcessWindow_Closed;
		Loaded += MainWindow_Loaded;
	}

	private void MainWindow_Loaded(object sender, EventArgs e) {
		if (DataContext is ProcessViewModel processViewModel) {
			processViewModel.CopiedNotificationRequested += ShowCopiedNotification;
			processViewModel.CopiedNotificationCurrentElementSaveStateRequested += ShowCopiedNotificationCurrentElementSaveState;
		}
	}

	private void ShowCopiedNotification() => ShowCopiedNotification(CopiedNotificationGrid);

	private void ShowCopiedNotificationCurrentElementSaveState() => ShowCopiedNotification(CopiedNotificationCurrentElementSaveStateGrid);

	private static async void ShowCopiedNotification(Grid ShowCopiedNotification) {
		ShowCopiedNotification.Visibility = Visibility.Visible;
		DoubleAnimation animation = new(1, 0, TimeSpan.FromSeconds(1));
		ShowCopiedNotification.BeginAnimation(OpacityProperty, animation);
		await Task.Delay(1000);
		ShowCopiedNotification.Visibility = Visibility.Collapsed;
	}

	private void ProcessWindow_Closed(object? sender, EventArgs e) {
		if (Application.Current.Windows.Count >= 1 && Application.Current.MainWindow is StartupWindow) { // On WPF debug, there is a secondary AdornerWindow attached to the process
																										 // this may be the case in other places aswell,
																										 // there is no need to check for window singleness,
																										 // only that at least the current MainWindow is the StartupWindow (Bug report)
			ExecuteClosingCommand();
		}
	}

	private void SelectWindowClick(object sender, RoutedEventArgs e) => (Application.Current.MainWindow as StartupWindow)?.Show();

	private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
		if (DataContext is ProcessViewModel processViewModel)
			processViewModel.SelectedItem = e.NewValue as ElementViewModel;
	}

	private async void InvokePatternActionHandler(object sender, RoutedEventArgs e) {
		var vm = (sender as Button)?.DataContext as PatternItem;

		if (vm?.Action != null)
			await Task.Run(vm.Action);
	}

	private void TreeOnSelectionChanged(object sender, SelectionChangedEventArgs e) {
		var container = TreeViewControl.ItemContainerGenerator.ContainerFromItem(TreeViewControl.SelectedItem) as ListViewItem;
		container?.BringIntoView();
	}

	private void ToggleButton_Click(object sender, RoutedEventArgs e) {
		if (sender is not ToggleButton expandToggleButton || DataContext is not ProcessViewModel processViewModel || expandToggleButton.DataContext is not ElementViewModel elementViewModel)
			return;

		if (expandToggleButton.IsChecked == true)
			processViewModel?.ElementToSelectChanged(elementViewModel?.AutomationElement, true);
		else if (expandToggleButton.IsChecked == false)
			processViewModel.CollapseElement(elementViewModel);
	}

	private void ProcessWindowOnClosed(object? sender, EventArgs e) => ExecuteClosingCommand();

	private void ExecuteClosingCommand() {
		if (DataContext is ProcessViewModel processViewModel && processViewModel.ClosingCommand.CanExecute(DataContext))
			processViewModel.ClosingCommand.Execute(DataContext);
	}
}