using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using FlaUInspect.Core;
using FlaUInspect.Core.Logger;
using FlaUInspect.Settings;
using FlaUInspect.ViewModels;
using FlaUInspect.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FlaUInspect;

public partial class App {

    public static IServiceProvider Services { get; private set; } = null!;
    public static FlaUiAppOptions FlaUiAppOptions { get; } = new ();

    public static InternalLogger Logger { get; } = new ();

    private void ApplicationStart(object sender, StartupEventArgs e) {

        ServiceCollection services = new ();
        services.AddSingleton<ISettingsService<FlaUiAppSettings>>(_ => new JsonSettingsService<FlaUiAppSettings>(Path.Combine(AppContext.BaseDirectory, $"appsettings.json")));
        Services = services.BuildServiceProvider();

        ISettingsService<FlaUiAppSettings> settingsService = Services.GetRequiredService<ISettingsService<FlaUiAppSettings>>();
        FlaUiAppSettings flaUiAppSettings = settingsService.Load();
        ApplyAppOption(flaUiAppSettings);

        //InternalLogger logger = new ();
        Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        StartupViewModel startupViewModel = new ();
        StartupWindow startupWindow = new (Logger) { DataContext = startupViewModel };
        Current.MainWindow = startupWindow;
        startupWindow.Show();
        Task.Run(() => startupViewModel.Init());

        return;
        AssemblyFileVersionAttribute? versionAttribute = Assembly.GetEntryAssembly()?.GetCustomAttribute(typeof(AssemblyFileVersionAttribute)) as AssemblyFileVersionAttribute;
        string applicationVersion = versionAttribute?.Version ?? "N/A";
        string windowHandle = string.Empty;

        if (e.Args.Length > 0) {
            windowHandle = e.Args[0];
        }

#if AUTOMATION_UIA3
        MainViewModel mainViewModel = new (AutomationType.UIA3, applicationVersion, windowHandle, logger);
        MainWindow mainWindow = new () { DataContext = mainViewModel };

        //Re-enable normal shutdown mode.
        Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        Current.MainWindow = mainWindow;
        mainWindow.Show();
#elif AUTOMATION_UIA2
        MainViewModel mainViewModel = new (AutomationType.UIA2, applicationVersion, windowHandle, logger);
        MainWindow mainWindow = new() { DataContext = mainViewModel };
        
        //Re-enable normal shutdown mode.
        Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        Current.MainWindow = mainWindow;
        mainWindow.Show();
#else
        // Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        // ChooseVersionWindow dialog = new ();
        //
        // if (dialog.ShowDialog() == true) {
        //
        //     MainViewModel mainViewModel = new (dialog.SelectedAutomationType, applicationVersion, windowHandle, Logger);
        //     MainWindow mainWindow = new () { DataContext = mainViewModel };
        //
        //     //Re-enable normal shutdown mode.
        //     Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        //     Current.MainWindow = mainWindow;
        //     mainWindow.Show();
        // }
#endif
    }

    public static void ApplyAppOption(FlaUiAppSettings settings) {
        // Apply theme
        Current.Dispatcher.Invoke(() => {
            ResourceDictionary newTheme = new ();

            // switch (settings.Theme) {
            //     case "Dark":
            //         newTheme.Source = new Uri("/FlaUInspect;component/Themes/DarkTheme.xaml", UriKind.Relative);
            //         break;
            //     default:
            //         newTheme.Source = new Uri("/FlaUInspect;component/Themes/LightTheme.xaml", UriKind.Relative);
            //         break;
            // }

            // Remove existing theme dictionaries
            for (int i = Current.Resources.MergedDictionaries.Count - 1; i >= 0; i--) {
                ResourceDictionary dict = Current.Resources.MergedDictionaries[i];

                if (dict.Source != null && (dict.Source.OriginalString.Contains("Themes/DarkTheme.xaml") || dict.Source.OriginalString.Contains("Themes/LightTheme.xaml"))) {
                    Current.Resources.MergedDictionaries.RemoveAt(i);
                }
            }

            // Add the new theme dictionary
            Current.Resources.MergedDictionaries.Add(newTheme);
        });

        ThicknessConverter converter = new ();
        FlaUiAppSettings cloneSetting = settings.Clone() as FlaUiAppSettings;

        if (settings.HoverOverlay != null) {
            Thickness hoverMargin = (Thickness)converter.ConvertFromString(cloneSetting.HoverOverlay.Margin);
            FlaUiAppOptions.HoverOverlay = () => new ElementOverlay(
                new ElementOverlayConfiguration(cloneSetting.HoverOverlay.Size,
                                                hoverMargin,
                                                ColorTranslator.FromHtml(cloneSetting.HoverOverlay.OverlayColor),
                                                ElementOverlay.GetRectangleFactory(cloneSetting.HoverOverlay.OverlayMode)));
        } else {
            FlaUiAppOptions.HoverOverlay = FlaUiAppOptions.DefaultOverlay;
        }

        if (settings.SelectionOverlay != null) {
            Thickness selectionMargin = (Thickness)converter.ConvertFromString(cloneSetting.SelectionOverlay.Margin);
            FlaUiAppOptions.SelectionOverlay = () => new ElementOverlay(
                new ElementOverlayConfiguration(cloneSetting.SelectionOverlay.Size,
                                                selectionMargin,
                                                ColorTranslator.FromHtml(cloneSetting.SelectionOverlay.OverlayColor),
                                                ElementOverlay.GetRectangleFactory(cloneSetting.SelectionOverlay.OverlayMode)));
        } else {
            FlaUiAppOptions.SelectionOverlay = FlaUiAppOptions.DefaultOverlay;
        }

        if (settings.PickOverlay != null) {
            Thickness pickMargin = (Thickness)converter.ConvertFromString(cloneSetting.PickOverlay.Margin);
            FlaUiAppOptions.PickOverlay = () => new ElementOverlay(
                new ElementOverlayConfiguration(cloneSetting.PickOverlay.Size,
                                                pickMargin,
                                                ColorTranslator.FromHtml(cloneSetting.PickOverlay.OverlayColor),
                                                ElementOverlay.GetRectangleFactory(cloneSetting.PickOverlay.OverlayMode)));
        } else {
            FlaUiAppOptions.PickOverlay = FlaUiAppOptions.DefaultOverlay;
        }
    }
}