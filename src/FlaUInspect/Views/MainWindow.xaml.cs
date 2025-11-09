using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using FlaUInspect.ViewModels;

namespace FlaUInspect.Views;

public partial class MainWindow {
    public MainWindow() {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, EventArgs e) {
        if (DataContext is MainViewModel mainViewModel) {
            mainViewModel.Initialize();
            mainViewModel.CopiedNotificationRequested += ShowCopiedNotification;
            mainViewModel.CopiedNotificationCurrentElementSaveStateRequested += ShowCopiedNotificationCurrentElementSaveStateRequested;
        }
    }

    private async void ShowCopiedNotificationCurrentElementSaveStateRequested() {
        CopiedNotificationCurrentElementSaveStateGrid.Visibility = Visibility.Visible;
        var animation = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(1));
        CopiedNotificationCurrentElementSaveStateGrid.BeginAnimation(UIElement.OpacityProperty, animation);
        await Task.Delay(1000);
        CopiedNotificationCurrentElementSaveStateGrid.Visibility = Visibility.Collapsed;
    }

    private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
        if (DataContext is MainViewModel mainViewModel) {
            mainViewModel.SelectedItem = e.NewValue as ElementViewModel;

            if (sender is TreeViewItem item) {
                item.BringIntoView();
                e.Handled = true;
            }
        }
    }

    private void InvokePatternActionHandler(object sender, RoutedEventArgs e) {
        PatternItem? vm = (PatternItem)((Button)sender).DataContext;

        if (vm.Action != null) {
            Task.Run(() => {
                vm.Action();
            });
        }
    }
    
    private async void ShowCopiedNotification() {
        CopiedNotificationGrid.Visibility = Visibility.Visible;
        var animation = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(1));
        CopiedNotificationGrid.BeginAnimation(UIElement.OpacityProperty, animation);
        await Task.Delay(1000);
        CopiedNotificationGrid.Visibility = Visibility.Collapsed;
    }
}