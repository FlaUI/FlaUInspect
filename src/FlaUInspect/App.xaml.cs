using System.Reflection;
using System.Windows;
using FlaUInspect.Core.Logger;
using FlaUInspect.ViewModels;
using FlaUInspect.Views;

namespace FlaUInspect;

public partial class App {
    private void ApplicationStart(object sender, StartupEventArgs e) {
        AssemblyInformationalVersionAttribute? versionAttribute = Assembly.GetEntryAssembly()?.GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;

        InternalLogger logger = new ();

#if AUTOMATION_UIA3
        MainViewModel mainViewModel = new (AutomationType.UIA3, logger);
        MainWindow mainWindow = new () { DataContext = mainViewModel };

        if (versionAttribute != null) {
            mainWindow.Title += " version" + versionAttribute.InformationalVersion;
        }

        //Re-enable normal shutdown mode.
        Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        Current.MainWindow = mainWindow;
        mainWindow.Show();
#elif AUTOMATION_UIA2
        MainViewModel mainViewModel = new (AutomationType.UIA2, logger);
        MainWindow mainWindow = new() { DataContext = mainViewModel };

        if (versionAttribute != null) {
            mainWindow.Title += " version" + versionAttribute.InformationalVersion;
        }

        //Re-enable normal shutdown mode.
        Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        Current.MainWindow = mainWindow;
        mainWindow.Show();
#else
        Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        ChooseVersionWindow dialog = new ();

        if (dialog.ShowDialog() == true) {
            MainViewModel mainViewModel = new (dialog.SelectedAutomationType, logger);
            MainWindow mainWindow = new() { DataContext = mainViewModel };

            if (versionAttribute != null) {
                mainWindow.Title += " version" + versionAttribute.InformationalVersion;
            }

            //Re-enable normal shutdown mode.
            Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            Current.MainWindow = mainWindow;
            mainWindow.Show();
        }
#endif
    }
}