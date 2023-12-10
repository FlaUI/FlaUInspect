using System.Configuration;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FlaUI.Core;
using FlaUInspect.ViewModels;

namespace FlaUInspect.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            AppendVersionToTitle();
            Height = 550;
            Width = 700;
            Loaded += MainWindow_Loaded;
            _vm = new MainViewModel();
            DataContext = _vm;
        }

        private void AppendVersionToTitle()
        {
            var attr = Assembly.GetEntryAssembly().GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;
            if (attr != null)
            {
                Title += " v" + attr.InformationalVersion;
            }
        }

        private void MainWindow_Loaded(object sender, System.EventArgs e)
        {
            if (!_vm.IsInitialized)
            {
                // Start application if version is saved
                var version = ConfigurationManager.AppSettings["version"];
                if (version == "2")
                {
                    _vm.Initialize(AutomationType.UIA2);
                }
                else if (version == "3")
                {
                    _vm.Initialize(AutomationType.UIA3);
                }
                else
                {
                    var dlg = new ChooseVersionWindow { Owner = this };
                    if (dlg.ShowDialog() != true)
                    {
                        Close();
                    }
                    // Save selected UIA version if dialog is not closed
                    else if (dlg.SelectedAutomationType == AutomationType.UIA2 
                                || dlg.SelectedAutomationType == AutomationType.UIA3)
                    {
                        Configuration config = ConfigurationManager.OpenExeConfiguration(System.Windows.Forms.Application.ExecutablePath);
                        config.AppSettings.Settings.Remove("version");
                        config.AppSettings.Settings.Add("version", dlg.SelectedAutomationType == AutomationType.UIA2 ? "2" : "3");
                        config.Save(ConfigurationSaveMode.Minimal);
                    }

                    _vm.Initialize(dlg.SelectedAutomationType);
                }
                Loaded -= MainWindow_Loaded;
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TreeViewSelectedHandler(object sender, RoutedEventArgs e)
        {
            var item = sender as TreeViewItem;
            if (item != null)
            {
                item.BringIntoView();
                e.Handled = true;
            }
        }
        private void InvokePatternActionHandler(object sender, RoutedEventArgs e)
        {
            DetailViewModel vm = (DetailViewModel)((Button)sender).DataContext;
            if (vm.ActionToExecute != null)
            {
                Task.Run(() =>
                {
                    vm.ActionToExecute();
                });
            }
        }
    }
}
