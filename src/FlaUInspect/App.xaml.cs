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

#if AUTOMATION_UIA3
        MainViewModel mainViewModel = new (AutomationType.UIA3, applicationVersion, logger);
        MainWindow mainWindow = new () { DataContext = mainViewModel };

        //Re-enable normal shutdown mode.
        Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        Current.MainWindow = mainWindow;
        mainWindow.Show();
#elif AUTOMATION_UIA2
        MainViewModel mainViewModel = new (AutomationType.UIA2, applicationVersion, logger);
        MainWindow mainWindow = new() { DataContext = mainViewModel };
        
        //Re-enable normal shutdown mode.
        Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        Current.MainWindow = mainWindow;
        mainWindow.Show();
#else
        Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        ChooseVersionWindow dialog = new ();

        if (dialog.ShowDialog() == true) {

            MainViewModel mainViewModel = new (dialog.SelectedAutomationType, applicationVersion, logger);
            MainWindow mainWindow = new () { DataContext = mainViewModel };

            //Re-enable normal shutdown mode.
            Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            Current.MainWindow = mainWindow;
            mainWindow.Show();
        }
#endif
    }
}