using System.Linq;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using FlaUI.Core.Definitions;

namespace FlaUInspect.Controls;

public partial class FindControl : UserControl {

    // ── Events ────────────────────────────────────────────────────────────────
    public event EventHandler<FindCriteria>? FindNextRequested;
    public event EventHandler<FindCriteria>? FindPrevRequested;
    public event EventHandler? CriteriaChanged;

    // ── Constructor ───────────────────────────────────────────────────────────
    public FindControl() {
        InitializeComponent();

        ControlTypeCombobox.ItemsSource = Enum.GetNames(typeof(ControlType));
        ControlTypeCombobox.SelectedIndex = 0;

        FrameworkTypeCombobox.ItemsSource = Enum.GetNames(typeof(FlaUI.Core.FrameworkType));
        FrameworkTypeCombobox.SelectedIndex = 0;

        // Apply correct visibility for the initially checked RadioButton (AutomationId)
        UpdateCriteriaVisibility("AutomationId");
    }

    // ── Public API ────────────────────────────────────────────────────────────
    public FindCriteria GetCurrentCriteria() {
        string findBy = FindByPanel.Children.OfType<RadioButton>()
                                   .FirstOrDefault(rb => rb.IsChecked == true)
                                   ?.Tag as string ?? "AutomationId";

        string matchTag = MatchModePanel.Children.OfType<RadioButton>()
                                        .FirstOrDefault(rb => rb.IsChecked == true)
                                        ?.Tag as string ?? "Substring";

        var criteria = new FindCriteria {
            FindBy    = findBy,
            MatchMode = matchTag switch {
                "Exact"     => SearchMatchMode.Exact,
                "IgnoreCase" => SearchMatchMode.IgnoreCase,
                _            => SearchMatchMode.Substring
            },
            SearchInChildrenOnly = SearchInChildrenCheckBox.IsChecked == true,
            SearchInLoadedOnly   = SearchInLoadedCheckBox.IsChecked   == true
        };

        switch (findBy) {
            case "ControlType":
                if (Enum.TryParse<ControlType>(ControlTypeCombobox.SelectedItem as string, out var ct))
                    criteria.ControlTypeValue = ct;
                break;
            case "FrameworkType":
                if (Enum.TryParse<FlaUI.Core.FrameworkType>(
                        FrameworkTypeCombobox.SelectedItem as string, out var ft))
                    criteria.FrameworkTypeValue = ft;
                break;
            default:
                criteria.TextValue = CriteriaTextBox.Text;
                break;
        }

        return criteria;
    }

    // ── Handlers ──────────────────────────────────────────────────────────────
    private void FindByRadioChanged(object sender, System.Windows.RoutedEventArgs e) {
        if (sender is RadioButton rb) {
            UpdateCriteriaVisibility(rb.Tag as string);
            CriteriaChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void MatchModeRadioChanged(object sender, System.Windows.RoutedEventArgs e) {
        CriteriaChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateCriteriaVisibility(string? findBy) {
        // Guard: controls may not yet be created during XAML init
        if (CriteriaTextBox == null) return;

        switch (findBy) {
            case "ControlType":
                CriteriaTextBox.Visibility    = System.Windows.Visibility.Collapsed;
                ControlTypeCombobox.Visibility  = System.Windows.Visibility.Visible;
                FrameworkTypeCombobox.Visibility = System.Windows.Visibility.Collapsed;
                MatchModePanel.Visibility       = System.Windows.Visibility.Collapsed;
                break;
            case "FrameworkType":
                CriteriaTextBox.Visibility    = System.Windows.Visibility.Collapsed;
                ControlTypeCombobox.Visibility  = System.Windows.Visibility.Collapsed;
                FrameworkTypeCombobox.Visibility = System.Windows.Visibility.Visible;
                MatchModePanel.Visibility       = System.Windows.Visibility.Collapsed;
                break;
            case "ProcessId":
                CriteriaTextBox.Visibility    = System.Windows.Visibility.Visible;
                ControlTypeCombobox.Visibility  = System.Windows.Visibility.Collapsed;
                FrameworkTypeCombobox.Visibility = System.Windows.Visibility.Collapsed;
                MatchModePanel.Visibility       = System.Windows.Visibility.Collapsed;
                break;
            default:
                CriteriaTextBox.Visibility    = System.Windows.Visibility.Visible;
                ControlTypeCombobox.Visibility  = System.Windows.Visibility.Collapsed;
                FrameworkTypeCombobox.Visibility = System.Windows.Visibility.Collapsed;
                MatchModePanel.Visibility       = System.Windows.Visibility.Visible;
                break;
        }
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

    private void FindNextClick(object sender, System.Windows.RoutedEventArgs e) {
        FindNextRequested?.Invoke(this, GetCurrentCriteria());
    }

    private void FindPrevClick(object sender, System.Windows.RoutedEventArgs e) {
        FindPrevRequested?.Invoke(this, GetCurrentCriteria());
    }
}
