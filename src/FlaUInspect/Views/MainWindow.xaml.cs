using System.Windows;
using System.Windows.Controls;
using FlaUInspect.ViewModels;

namespace FlaUInspect.Views;

public partial class MainWindow {
    public MainWindow() {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }
    public MainViewModel? vm => DataContext as MainViewModel;
    private void MainWindow_Loaded(object sender, EventArgs e) {
        vm?.Initialize();
    }

    private async void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
        if (vm != null) {
            vm.SelectedItem = e.NewValue as ElementViewModel;

            if (sender is TreeViewItem item) {
                item.BringIntoView();
                e.Handled = true;
            } else if (sender is TreeView tv) {
                var ci = ContainerFromItemRecursive(tv.ItemContainerGenerator, e.NewValue);
                await Task.Delay(25);
                if (ci is TreeViewItem tvi && centerNextSelectedItem) { // we only do this when we know it is going to badly scroll so this should be slightly better
                    centerNextSelectedItem = false;
                    BringIntoViewCentered(tvi);
                }

                e.Handled = true;
            }
        }
    }

    private void BringIntoViewCentered(FrameworkElement element) {
        if (element == null) return;

        element.BringIntoView();

        var scrollViewer = GetScrollViewer(element);
        if (scrollViewer == null) return;

        var elementPoint = element.TransformToAncestor(scrollViewer).Transform(new Point(0, 0));
        var scrollTarget = scrollViewer.VerticalOffset + elementPoint.Y - (scrollViewer.ViewportHeight / 2) + (element.ActualHeight / 2);

        scrollViewer.ScrollToVerticalOffset(scrollTarget);
    }

    private ScrollViewer? GetScrollViewer(DependencyObject o) {
        DependencyObject? parent = o;
        while (parent != null) {
            if (parent is ScrollViewer sv) return sv;
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        }
        return null;
    }
    public static TreeViewItem ContainerFromItemRecursive(ItemContainerGenerator root, object item) {
        if (root == null)
            return null;

        var treeViewItem = root.ContainerFromItem(item) as TreeViewItem;
        if (treeViewItem != null)
            return treeViewItem;
        foreach (var subItem in root.Items) {
            treeViewItem = root.ContainerFromItem(subItem) as TreeViewItem;
            var search = ContainerFromItemRecursive(treeViewItem?.ItemContainerGenerator, item);
            if (search != null)
                return search;
        }
        return null;
    }

    private void InvokePatternActionHandler(object sender, RoutedEventArgs e) {
        PatternItem? vm = (PatternItem)((Button)sender).DataContext;

        if (vm.Action != null) {
            Task.Run(() => {
                vm.Action();
            });
        }
    }
    private bool centerNextSelectedItem;
    private void TreeViewItem_ContextMenuOpening(object sender, ContextMenuEventArgs e) {
        var dc = (e.Source as FrameworkElement)?.DataContext;
        if (dc == null)
            return;
        if (dc is ElementViewModel evm && evm.IsSelected == false) {
            centerNextSelectedItem = true;
            evm.IsSelected = true;
        }
    }
}