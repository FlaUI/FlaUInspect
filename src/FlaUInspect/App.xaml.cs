using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using FlaUI.Core;
using FlaUInspect.Core.Logger;
using FlaUInspect.ViewModels;
using FlaUInspect.Views;

namespace FlaUInspect;

public partial class App {
    private void ApplicationStart(object sender, StartupEventArgs e) {
        var args = Environment.GetCommandLineArgs();
        string? processName = null;
        string? processPid = null;
        string? exportFile = null;
        string? exportOptions = null;
        string? errorToFile = null;

        for (int i = 0; i < args.Length; i++) {
            if (args[i] == "--process" && i + 1 < args.Length) {
                processName = args[++i];
            }
            if (args[i] == "--pid" && i + 1 < args.Length) {
                processPid = args[++i];
            }
            if (args[i] == "--export_json") {
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-")) {
                    exportFile = args[++i];
                } else {
                    exportFile = "export.json";
                }
            }
            if (args[i] == "--export_json_options" && i + 1 < args.Length) {
                exportOptions = args[++i].Trim();
            }
            if (args[i] == "--error_file" && i + 1 < args.Length) {
                errorToFile = args[++i];
            }
        }

        if ((!String.IsNullOrWhiteSpace(processName) || !String.IsNullOrWhiteSpace(processPid)) && exportFile != null) {
            try {
                using var automation = new FlaUI.UIA3.UIA3Automation();
                var process = String.IsNullOrWhiteSpace(processPid) ? System.Diagnostics.Process.GetProcessesByName(processName).FirstOrDefault() : System.Diagnostics.Process.GetProcessById(int.Parse(processPid));
                if (process == null)
                    throw new ArgumentException($"Process: {processName} {processPid} not found");

                var app = FlaUI.Core.Application.Attach(process);
                var window = app.GetMainWindow(automation);
                if (window != null) {
                    HashSet<string> options = new(Core.JsonExporter.DefaultOptions.Select(a => a.ToString()), StringComparer.OrdinalIgnoreCase);
                    if (exportOptions != null) {
                        if (!exportOptions.StartsWith("+"))
                            options.Clear();
                        else
                            exportOptions = exportOptions.Substring(1);
                        var parts = exportOptions.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        foreach (var part in parts)
                            options.Add(part);
                    }

                    var data = FlaUInspect.Core.JsonExporter.CollectNodeData(window, options);
                    var json = FlaUInspect.Core.JsonExporter.SerializeNodeInfo(data);
                    System.IO.File.WriteAllText(exportFile, json);
                }

            } catch (Exception ex) {
                var msg = $"Error exporting: {ex.Message}";
                if (String.IsNullOrWhiteSpace(errorToFile))
                    MessageBox.Show(msg, "FlaUI CLI Export Failed");
                else
                    System.IO.File.WriteAllText(errorToFile, msg);
                Environment.Exit(1);
            }
            Current.Shutdown(0);
            return;
        }

        AssemblyFileVersionAttribute? versionAttribute = Assembly.GetEntryAssembly()?.GetCustomAttribute(typeof(AssemblyFileVersionAttribute)) as AssemblyFileVersionAttribute;
        string applicationVersion = versionAttribute?.Version ?? "N/A";
        InternalLogger logger = new();


        Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        ChooseVersionWindow dialog = new();
#if AUTOMATION_UIA3
		dialog.SelectedAutomationType = AutomationType.UIA3;
#elif AUTOMATION_UIA2
		dialog.SelectedAutomationType = AutomationType.UIA2;
#else
        if (args.Any(a => a.Equals("--uia2", StringComparison.OrdinalIgnoreCase)))
            dialog.SelectedAutomationType = AutomationType.UIA2;
        else if (args.Any(a => a.Equals("--uia3", StringComparison.OrdinalIgnoreCase)))
            dialog.SelectedAutomationType = AutomationType.UIA3;
        else if (dialog.ShowDialog() != true)
            return;
#endif



        MainViewModel mainViewModel = new(dialog.SelectedAutomationType, applicationVersion, logger);
        MainWindow mainWindow = new() { DataContext = mainViewModel };

        //Re-enable normal shutdown mode.
        Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        Current.MainWindow = mainWindow;
        mainWindow.Show();


    }
}