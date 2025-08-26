using System.Windows;
using System.Windows.Controls;
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
        }
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
        var vm = (PatternItem)((Button)sender).DataContext;
        if (vm.Action != null) {
            Task.Run(() =>
            {
                vm.Action();
            });
        }
    }
}