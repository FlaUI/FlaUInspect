﻿using System.Reflection;
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
#if AUTOMATION_UIA3
                _vm.Initialize(AutomationType.UIA3);
#elif AUTOMATION_UIA2
                _vm.Initialize(AutomationType.UIA2);
#else
                var dlg = new ChooseVersionWindow { Owner = this };
                if (dlg.ShowDialog() != true)
                {
                    Close();
                }
                _vm.Initialize(dlg.SelectedAutomationType);
                Loaded -= MainWindow_Loaded;
#endif
                Title += " - " + _vm.SelectedAutomationType.ToString();
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
    }
}
