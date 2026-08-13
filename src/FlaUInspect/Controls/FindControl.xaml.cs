using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FlaUI.Core;
using FlaUI.Core.Definitions;

namespace FlaUInspect.Controls;

public partial class FindControl : UserControl {

    // ── Events ────────────────────────────────────────────────────────────────
    public event EventHandler<FindCriteria>? FindNextRequested;
    public event EventHandler<FindCriteria>? FindPrevRequested;

    /// <summary>Fired whenever the user changes any search field (criteria reset).</summary>
    public event EventHandler? CriteriaChanged;

    // ── Constructor ───────────────────────────────────────────────────────────
    public FindControl() {
        InitializeComponent();

        FindByComboBox.ItemsSource = new[] {
            "AutomationId", "ControlType", "ClassName", "Name",
            "Text", "FrameworkId", "FrameworkType", "ProcessId",
            "LocalizedControlType", "HelpText", "Value"
        };
        FindByComboBox.SelectedIndex = 0;

        PropertyConditionFlagsComboBox.ItemsSource = Enum.GetNames(typeof(PropertyConditionFlags));
        PropertyConditionFlagsComboBox.SelectedIndex = 0;

        ControlTypeCombobox.ItemsSource = Enum.GetNames(typeof(ControlType));
        ControlTypeCombobox.SelectedIndex = 0;

        FrameworkTypeCombobox.ItemsSource = Enum.GetNames(typeof(FrameworkType));
        FrameworkTypeCombobox.SelectedIndex = 0;
    }

    // ── Public API ────────────────────────────────────────────────────────────
    public FindCriteria GetCurrentCriteria() {
        FindCriteria criteria = new () {
            FindBy = FindByComboBox.SelectedItem as string ?? "AutomationId",
            IgnoreCase = PropertyConditionFlagsComboBox.SelectedItem as string == "IgnoreCase",
            SearchInChildrenOnly = SearchInChildrenCheckBox.IsChecked == true,
            SearchInLoadedOnly = SearchInLoadedCheckBox.IsChecked == true
        };

        switch (criteria.FindBy) {
            case "ControlType":
                if (Enum.TryParse<ControlType>(ControlTypeCombobox.SelectedItem as string, out ControlType ct)) {
                    criteria.ControlTypeValue = ct;
                }
                break;
            case "FrameworkType":
                if (Enum.TryParse<FrameworkType>(FrameworkTypeCombobox.SelectedItem as string, out FrameworkType ft)) {
                    criteria.FrameworkTypeValue = ft;
                }
                break;
            default:
                criteria.TextValue = CriteriaTextBox.Text;
                break;
        }

        return criteria;
    }

    // ── Handlers ──────────────────────────────────────────────────────────────
    private void FindByComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (e.AddedItems.Count == 0) return;
        var selected = e.AddedItems[0] as string;

        switch (selected) {
            case "ControlType":
                CriteriaTextBox.Visibility = Visibility.Collapsed;
                ControlTypeCombobox.Visibility = Visibility.Visible;
                FrameworkTypeCombobox.Visibility = Visibility.Collapsed;
                PropertyConditionFlagsComboBox.Visibility = Visibility.Collapsed;
                break;
            case "FrameworkType":
                CriteriaTextBox.Visibility = Visibility.Collapsed;
                ControlTypeCombobox.Visibility = Visibility.Collapsed;
                FrameworkTypeCombobox.Visibility = Visibility.Visible;
                PropertyConditionFlagsComboBox.Visibility = Visibility.Collapsed;
                break;
            case "ProcessId":
                CriteriaTextBox.Visibility = Visibility.Visible;
                ControlTypeCombobox.Visibility = Visibility.Collapsed;
                FrameworkTypeCombobox.Visibility = Visibility.Collapsed;
                PropertyConditionFlagsComboBox.Visibility = Visibility.Collapsed;
                break;
            default:
                CriteriaTextBox.Visibility = Visibility.Visible;
                ControlTypeCombobox.Visibility = Visibility.Collapsed;
                FrameworkTypeCombobox.Visibility = Visibility.Collapsed;
                PropertyConditionFlagsComboBox.Visibility = Visibility.Visible;
                break;
        }

        CriteriaChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CriteriaTextBoxTextChanged(object sender, TextChangedEventArgs e) {
        CriteriaChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ConditionInputChanged(object sender, SelectionChangedEventArgs e) {
        CriteriaChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CriteriaTextBoxKeyDown(object sender, KeyEventArgs e) {
        if (e.Key == Key.Enter) {
            if (Keyboard.Modifiers == ModifierKeys.Shift)
                FindPrevRequested?.Invoke(this, GetCurrentCriteria());
            else
                FindNextRequested?.Invoke(this, GetCurrentCriteria());
            e.Handled = true;
        }
    }

    private void FindNextClick(object sender, RoutedEventArgs e) {
        FindNextRequested?.Invoke(this, GetCurrentCriteria());
    }

    private void FindPrevClick(object sender, RoutedEventArgs e) {
        FindPrevRequested?.Invoke(this, GetCurrentCriteria());
    }
}
