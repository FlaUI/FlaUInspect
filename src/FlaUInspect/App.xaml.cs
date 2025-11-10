using System.Reflection;
using System.Windows;
using FlaUI.Core;
using FlaUInspect.Core.Logger;
using FlaUInspect.ViewModels;
using FlaUInspect.Views;

namespace FlaUInspect;

public partial class App {
    private void ApplicationStart(object sender, StartupEventArgs e) {
        AssemblyFileVersionAttribute? versionAttribute = Assembly.GetEntryAssembly()?.GetCustomAttribute(typeof(AssemblyFileVersionAttribute)) as AssemblyFileVersionAttribute;
        string applicationVersion = versionAttribute?.Version ?? "N/A";
        InternalLogger logger = new ();
        string applicationName = string.Empty;
        if (e.Args.Length > 0) {
            applicationName = e.Args[0];
        }

#if AUTOMATION_UIA3
        MainViewModel mainViewModel = new (AutomationType.UIA3, applicationVersion, applicationName, logger);
        MainWindow mainWindow = new () { DataContext = mainViewModel };

        //Re-enable normal shutdown mode.
        Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        Current.MainWindow = mainWindow;
        mainWindow.Show();
#elif AUTOMATION_UIA2
        MainViewModel mainViewModel = new (AutomationType.UIA2, applicationVersion, applicationName, logger);
        MainWindow mainWindow = new() { DataContext = mainViewModel };
        
        //Re-enable normal shutdown mode.
        Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        Current.MainWindow = mainWindow;
        mainWindow.Show();
#else
        Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        ChooseVersionWindow dialog = new ();

        if (dialog.ShowDialog() == true) {

            MainViewModel mainViewModel = new (dialog.SelectedAutomationType, applicationVersion, applicationName, logger);
            MainWindow mainWindow = new () { DataContext = mainViewModel };

            //Re-enable normal shutdown mode.
            Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            Current.MainWindow = mainWindow;
            mainWindow.Show();
        }
#endif
    }
}