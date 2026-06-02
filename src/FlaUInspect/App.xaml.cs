using System.IO;
using System.Windows;
using FlaUInspect.Core.Logger;
using FlaUInspect.Settings;
using FlaUInspect.ViewModels;
using FlaUInspect.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FlaUInspect;

public partial class App {

	public static IServiceProvider Services { get; private set; } = null!;
	public static FlaUiAppOptions FlaUiAppOptions { get; } = new();

	public static InternalLogger Logger { get; } = new();

	protected override async void OnStartup(StartupEventArgs e) {
		base.OnStartup(e);

		ServiceCollection services = new();
		_ = services.AddSingleton<ISettingsService<FlaUiAppSettings>>(_ => new JsonSettingsService<FlaUiAppSettings>(Path.Combine(AppContext.BaseDirectory, $"appsettings.json")));
		Services = services.BuildServiceProvider();

		var settingsService = Services.GetRequiredService<ISettingsService<FlaUiAppSettings>>();
		var flaUiAppSettings = settingsService.Load();
		ApplyAppOption(flaUiAppSettings);

		//InternalLogger logger = new ();
		Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
		StartupViewModel startupViewModel = new();
		StartupWindow startupWindow = new(Logger) { DataContext = startupViewModel };
		Current.MainWindow = startupWindow;
		startupWindow.Show();

		//Preload light theme
		SetTheme(flaUiAppSettings);

		await Task.Run(startupViewModel.Init);
	}

	public static void ApplyAppOption(FlaUiAppSettings settings) {
		// Apply theme
		Current.Dispatcher.Invoke(() => SetTheme(settings));

		FlaUiAppOptions.HoverOverlay = settings.HoverOverlay != null
			? (() => new(settings.HoverOverlay))
			: FlaUiAppOptions.DefaultOverlay;

		FlaUiAppOptions.SelectionOverlay = settings.SelectionOverlay != null
			? (() => new(settings.SelectionOverlay))
			: FlaUiAppOptions.DefaultOverlay;

		FlaUiAppOptions.PickOverlay = settings.PickOverlay != null
			? (() => new(settings.PickOverlay))
			: FlaUiAppOptions.DefaultOverlay;
	}

	private static void SetTheme(FlaUiAppSettings settings) {
		ResourceDictionary newTheme = new() {
			Source = settings.Theme switch {
				"Dark" => new Uri("/FlaUInspect;component/Themes/DarkTheme.xaml", UriKind.Relative),
				_ => new Uri("/FlaUInspect;component/Themes/LightTheme.xaml", UriKind.Relative),
			}
		};

		// Remove existing theme dictionaries
		for (var i = Current.Resources.MergedDictionaries.Count - 1; i >= 0; i--) {
			var dict = Current.Resources.MergedDictionaries[i];

			if (dict.Source != null && (dict.Source.OriginalString.Contains("Themes/DarkTheme.xaml") || dict.Source.OriginalString.Contains("Themes/LightTheme.xaml")))
				Current.Resources.MergedDictionaries.RemoveAt(i);
		}

		// Add the new theme dictionary
		Current.Resources.MergedDictionaries.Add(newTheme);
	}
}