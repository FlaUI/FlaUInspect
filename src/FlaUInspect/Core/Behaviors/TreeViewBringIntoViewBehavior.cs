using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace FlaUInspect.Core.Behaviors;

public static class TreeViewBringIntoViewBehavior {
    public static readonly DependencyProperty BringSelectedItemIntoViewProperty =
        DependencyProperty.RegisterAttached(
            "BringSelectedItemIntoView",
            typeof(bool),
            typeof(TreeViewBringIntoViewBehavior),
            new PropertyMetadata(false, OnChanged));

    public static bool GetBringSelectedItemIntoView(DependencyObject obj) {
        return (bool)obj.GetValue(BringSelectedItemIntoViewProperty);
    }

    public static void SetBringSelectedItemIntoView(DependencyObject obj, bool value) {
        obj.SetValue(BringSelectedItemIntoViewProperty, value);
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        if (d is not TreeView treeView) {
            return;
        }

        if ((bool)e.NewValue) {
            treeView.SelectedItemChanged += TreeView_SelectedItemChanged;
        } else {
            treeView.SelectedItemChanged -= TreeView_SelectedItemChanged;
        }
    }

    private static void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
        if (sender is not TreeView treeView || e.NewValue == null) {
            return;
        }

        treeView.Dispatcher.BeginInvoke(new Action(() => {
                                            TreeViewItem? item = GetTreeViewItem(treeView, e.NewValue);
                                            item?.BringIntoView();
                                        }),
                                        DispatcherPriority.Background);
    }

    private static TreeViewItem? GetTreeViewItem(ItemsControl container, object item) {
        if (container.DataContext == item)
            return container as TreeViewItem;

        foreach (object? i in container.Items) {
            ItemsControl? child = container.ItemContainerGenerator.ContainerFromItem(i) as ItemsControl;

            if (child == null)
                continue;

            if (child is TreeViewItem tvi && !tvi.IsExpanded) {
                tvi.IsExpanded = true;
            }

            TreeViewItem? result = GetTreeViewItem(child, item);

            if (result != null) {
                return result;
            }
        }

        return null;
    }
}