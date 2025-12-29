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

        
        Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        ChooseVersionWindow dialog = new ();
#if AUTOMATION_UIA3
		dialog.SelectedAutomationType = AutomationType.UIA3;
#elif AUTOMATION_UIA2
		dialog.SelectedAutomationType = AutomationType.UIA2;
#else
		var args = Environment.GetCommandLineArgs();
		if (args.Any(a=>a.Equals("--uia2", StringComparison.OrdinalIgnoreCase)))
			dialog.SelectedAutomationType = AutomationType.UIA2;
		else if (args.Any(a=>a.Equals("--uia3", StringComparison.OrdinalIgnoreCase)))
			dialog.SelectedAutomationType = AutomationType.UIA3;
		else if (dialog.ShowDialog() != true)
			return;
#endif



            MainViewModel mainViewModel = new (dialog.SelectedAutomationType, applicationVersion, logger);
            MainWindow mainWindow = new () { DataContext = mainViewModel };

            //Re-enable normal shutdown mode.
            Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            Current.MainWindow = mainWindow;
            mainWindow.Show();


    }
}