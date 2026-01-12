using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using FlaUI.Core.AutomationElements;
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
            processViewModel.CopiedNotificationCurrentElementSaveStateRequested += ShowCopiedNotificationCurrentElementSaveStateRequested;
        }
    }

    private async void ShowCopiedNotification() {
        CopiedNotificationGrid.Visibility = Visibility.Visible;
        DoubleAnimation animation = new (1, 0, TimeSpan.FromSeconds(1));
        CopiedNotificationGrid.BeginAnimation(OpacityProperty, animation);
        await Task.Delay(1000);
        CopiedNotificationGrid.Visibility = Visibility.Collapsed;
    }

    private async void ShowCopiedNotificationCurrentElementSaveStateRequested() {
        CopiedNotificationCurrentElementSaveStateGrid.Visibility = Visibility.Visible;
        DoubleAnimation animation = new (1, 0, TimeSpan.FromSeconds(1));
        CopiedNotificationCurrentElementSaveStateGrid.BeginAnimation(OpacityProperty, animation);
        await Task.Delay(1000);
        CopiedNotificationCurrentElementSaveStateGrid.Visibility = Visibility.Collapsed;
    }

    private void ProcessWindow_Closed(object? sender, EventArgs e) {
        if (Application.Current.Windows.Count == 1 && Application.Current.MainWindow is StartupWindow startupWindow) {
            if (DataContext is ProcessViewModel processViewModel) {
                if (processViewModel.ClosingCommand.CanExecute(DataContext)) {
                    processViewModel.ClosingCommand.Execute(DataContext);
                }
            }
            
            startupWindow.Show();
        }
    }

    private void SelectWindowClick(object sender, RoutedEventArgs e) {
        (Application.Current.MainWindow as StartupWindow)?.Show();
    }

    private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
        if (DataContext is ProcessViewModel processViewModel) {
            processViewModel.SelectedItem = e.NewValue as ElementViewModel;
        }
    }

    private void InvokePatternActionHandler(object sender, RoutedEventArgs e) {
        PatternItem? vm = (sender as Button)?.DataContext as PatternItem;

        if (vm?.Action != null) {
            Task.Run(() => {
                vm.Action();
            });
        }
    }

    private void TreeOnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        ListViewItem? container = TreeViewControl.ItemContainerGenerator.ContainerFromItem(TreeViewControl.SelectedItem) as ListViewItem;
        container?.BringIntoView();
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e) {
        ToggleButton? expandToggleButton = sender as ToggleButton;

        if (expandToggleButton == null) {
            return;
        }

        ProcessViewModel? processViewModel = DataContext as ProcessViewModel;
        ElementViewModel? elementViewModel = expandToggleButton.DataContext as ElementViewModel;

        if (processViewModel == null) {
            return;
        }

        if (elementViewModel == null) {
            return;
        }

        if (expandToggleButton.IsChecked == true) {
            AutomationElement? automationElement = elementViewModel?.AutomationElement;
            processViewModel?.ElementToSelectChanged(automationElement, true);
        } else if (expandToggleButton.IsChecked == false) {
            processViewModel.CollapseElement(elementViewModel);
        }
    }

    private void ProcessWindowOnClosed(object? sender, EventArgs e) {
        if (DataContext is ProcessViewModel processViewModel) {
            if (processViewModel.ClosingCommand.CanExecute(DataContext)) {
                processViewModel.ClosingCommand.Execute(DataContext);
            }
        }
    }
}