using System.Windows;
using System.Windows.Controls;

namespace FlaUInspect.Core;

public static class TreeViewItemBringIntoViewBehavior {
	public static readonly DependencyProperty MonitorSelectionProperty =
		DependencyProperty.RegisterAttached(
			"MonitorSelection",
			typeof(bool),
			typeof(TreeViewItemBringIntoViewBehavior),
			new PropertyMetadata(false, OnMonitorSelectionChanged));

	public static bool GetMonitorSelection(DependencyObject obj) => (bool)obj.GetValue(MonitorSelectionProperty);

	public static void SetMonitorSelection(DependencyObject obj, bool value) => obj.SetValue(MonitorSelectionProperty, value);

	private static void OnMonitorSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		if (d is not TreeViewItem tvi || e.NewValue is not bool value)
			return;

		if (value)
			tvi.Selected += TreeViewItem_Selected;
		else
			tvi.Selected -= TreeViewItem_Selected;
	}

	private static void TreeViewItem_Selected(object sender, RoutedEventArgs e) {
		if (sender is TreeViewItem item)
			item.BringIntoView();
	}
}