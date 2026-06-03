using System.Windows.Controls;
using FlaUI.Core.Definitions;

namespace FlaUInspect.Controls;

public partial class FindControl : UserControl {
    public FindControl() {
        InitializeComponent();

        FindByComboBox.ItemsSource = new[] { "AutomationId", "ControlType", "ClassName", "Name", "Text", "FrameworkId", "FrameworkType", "ProcessId", "LocalizedControlType", "HelpText", "Value" };
        FindByComboBox.SelectedIndex = 0;
        PropertyConditionFlagsComboBox.ItemsSource = Enum.GetNames(typeof(PropertyConditionFlags));
        PropertyConditionFlagsComboBox.SelectedIndex = 0;
        ControlTypeCombobox.SelectedIndex = 0;
        FrameworkTypeCombobox.SelectedIndex = 0;
        ControlTypeCombobox.ItemsSource = Enum.GetNames(typeof(ControlType));
        FrameworkTypeCombobox.ItemsSource = Enum.GetNames(typeof(FlaUI.Core.FrameworkType));
    }

    private void FindByComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (e.AddedItems.Count > 0) {
            string? selectedItem = e.AddedItems[0] as string;

            switch (selectedItem) {
                case "ControlType":
                    CriteriaTextBox.Visibility = System.Windows.Visibility.Collapsed;
                    ControlTypeCombobox.Visibility = System.Windows.Visibility.Visible;
                    FrameworkTypeCombobox.Visibility = System.Windows.Visibility.Collapsed;
                    PropertyConditionFlagsComboBox.Visibility = System.Windows.Visibility.Collapsed;
                    break;
                case "FrameworkType":
                    CriteriaTextBox.Visibility = System.Windows.Visibility.Collapsed;
                    ControlTypeCombobox.Visibility = System.Windows.Visibility.Collapsed;
                    FrameworkTypeCombobox.Visibility = System.Windows.Visibility.Visible;
                    PropertyConditionFlagsComboBox.Visibility = System.Windows.Visibility.Collapsed;
                    break;
                case "ProcessId":
                    CriteriaTextBox.Visibility = System.Windows.Visibility.Visible;
                    ControlTypeCombobox.Visibility = System.Windows.Visibility.Collapsed;
                    FrameworkTypeCombobox.Visibility = System.Windows.Visibility.Collapsed;
                    PropertyConditionFlagsComboBox.Visibility = System.Windows.Visibility.Collapsed;
                    break;
                default:
                    CriteriaTextBox.Visibility = System.Windows.Visibility.Visible;
                    ControlTypeCombobox.Visibility = System.Windows.Visibility.Collapsed;
                    FrameworkTypeCombobox.Visibility = System.Windows.Visibility.Collapsed;
                    PropertyConditionFlagsComboBox.Visibility = System.Windows.Visibility.Visible;
                    break;
            }
        }
    }
}