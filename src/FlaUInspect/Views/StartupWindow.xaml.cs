using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using FlaUI.Core;
using FlaUI.UIA2;
using FlaUI.UIA3;
using FlaUInspect.Core;
using FlaUInspect.Core.Logger;
using FlaUInspect.ViewModels;
using Application = System.Windows.Application;

namespace FlaUInspect.Views;

public partial class StartupWindow {

    private const int GWL_STYLE = -16;
    private const int WS_SYSMENU = 0x80000;
    private const int MF_BYCOMMAND = 0x00000000;
    private const int SC_CLOSE = 0xF060;
    private readonly InternalLogger _logger;

    public StartupWindow(InternalLogger logger) {
        _logger = logger;
        InitializeComponent();
    }

    private void Uia2Click(object sender, RoutedEventArgs e) {
        OpenProcessWindow(new UIA2Automation());
    }

    private void Uia3Click(object sender, RoutedEventArgs e) {
        OpenProcessWindow(new UIA3Automation());
    }

    private void OpenProcessWindow(AutomationBase automationBase) {
        if (ProcessesListBox.SelectedItem is ProcessWindowInfo processWindowInfo) {
            HoverMouseInitialize();
            ProcessViewModel processViewModel = new (automationBase, processWindowInfo.ProcessId, processWindowInfo.MainWindowHandle, _logger);

            ProcessWindow processWindow = new () { DataContext = processViewModel };
            processWindow.Show();
            Task.Run(() => processViewModel.Initialize());
            Hide();
        }
    }

    private void HoverMouseInitialize() {
        if (!HoverManager.IsInitialized) {
            HoverManager.Initialize(new UIA3Automation(), () => App.FlaUiAppOptions.HoverOverlay(), _logger);
        }
    }

    private void CloseClick(object sender, RoutedEventArgs e) {
        if (Application.Current.Windows.Count == 1) {
            Close();
        } else {
            Hide();
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

    [DllImport("user32.dll")]
    private static extern bool RemoveMenu(IntPtr hMenu, uint uPosition, uint uFlags);

    protected override void OnSourceInitialized(EventArgs e) {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        IntPtr hMenu = GetSystemMenu(hwnd, false);
        RemoveMenu(hMenu, SC_CLOSE, MF_BYCOMMAND);
    }

    private void PickWindowButton_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
        if (sender is Button { Command: { } command } && command.CanExecute(null)) {
            command.Execute(null);
        }
    }

    private void ProcessesListBoxOnMouseDoubleClick(object sender, MouseButtonEventArgs e) {
        if (Uia2RadioButton.IsChecked == true) {
            OpenProcessWindow(new UIA2Automation());
        } else if (Uia3RadioButton.IsChecked == true) {
            OpenProcessWindow(new UIA3Automation());
        }
    }

}