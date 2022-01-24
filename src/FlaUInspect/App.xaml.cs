using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Windows;

namespace FlaUInspect
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static Lazy<Settings.FlaUInspect> configuration = new Lazy<Settings.FlaUInspect>(ReadConfiguration);

        public static new App Current => (App)Application.Current;

        public Settings.FlaUInspect Configuration => configuration.Value;

        private static Settings.FlaUInspect ReadConfiguration()
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

            // Read the configuration settings
            var setting = new Settings.FlaUInspect();
            configuration.Bind(nameof(Settings.FlaUInspect), setting);

            return setting;
        }
    }
}
