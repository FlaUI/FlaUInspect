using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA2;
using FlaUI.UIA3;
using FlaUInspect.Core;
using Microsoft.Win32;

namespace FlaUInspect.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private HoverMode _hoverMode;
        private FocusTrackingMode _focusTrackingMode;
        private ITreeWalker _treeWalker;
        private AutomationBase _automation;
        private AutomationElement _rootElement;

        public MainViewModel()
        {
            Elements = new ObservableCollection<ElementViewModel>();
            StartNewInstanceCommand = new RelayCommand(o =>
            {
                var info = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location);
                Process.Start(info);
            });
            StartNewInstanceWithVersionSelectionCommand = new RelayCommand(o =>
            {
                // Reset UIA version in configuration
                Configuration config = ConfigurationManager.OpenExeConfiguration(System.Windows.Forms.Application.ExecutablePath);
                config.AppSettings.Settings.Remove("version");
                config.Save(ConfigurationSaveMode.Minimal);

                var info = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location);
                Process.Start(info);
            });
            CaptureSelectedItemCommand = new RelayCommand(o =>
            {
                if (SelectedItemInTree == null)
                {
                    return;
                }
                var capturedImage = SelectedItemInTree.AutomationElement.Capture();
                var saveDialog = new SaveFileDialog();
                saveDialog.Filter = "Png file (*.png)|*.png";
                if (saveDialog.ShowDialog() == true)
                {
                    capturedImage.Save(saveDialog.FileName, ImageFormat.Png);
                }
                capturedImage.Dispose();
            });
            RefreshCommand = new RelayCommand(o =>
            {
                RefreshTree();
            });
        }

        public bool IsInitialized
        {
            get { return GetProperty<bool>(); }
            private set { SetProperty(value); }
        }

        public bool EnableHoverMode
        {
            get { return GetProperty<bool>(); }
            set
            {
                if (SetProperty(value))
                {
                    Configuration config = ConfigurationManager.OpenExeConfiguration(System.Windows.Forms.Application.ExecutablePath);
                    config.AppSettings.Settings.Remove("EnableHoverMode");
                    if (value)
                    {
                        config.AppSettings.Settings.Add("EnableHoverMode", "true");
                        _hoverMode.Start();
                    }
                    else
                    {
                        config.AppSettings.Settings.Add("EnableHoverMode", "false");
                        _hoverMode.Stop();
                    }
                    config.Save(ConfigurationSaveMode.Minimal);
                }
            }
        }

        public bool EnableFocusTrackingMode
        {
            get { return GetProperty<bool>(); }
            set
            {
                if (SetProperty(value))
                {
                    Configuration config = ConfigurationManager.OpenExeConfiguration(System.Windows.Forms.Application.ExecutablePath);
                    config.AppSettings.Settings.Remove("EnableFocusTrackingMode");
                    if (value)
                    {
                        config.AppSettings.Settings.Add("EnableFocusTrackingMode", "true");
                        _focusTrackingMode.Start();
                    }
                    else
                    {
                        config.AppSettings.Settings.Add("EnableFocusTrackingMode", "false");
                        _focusTrackingMode.Stop();
                    }
                    config.Save(ConfigurationSaveMode.Minimal);
                }
            }
        }

        public bool EnableXPath
        {
            get { return GetProperty<bool>(); }
            set
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(System.Windows.Forms.Application.ExecutablePath);
                config.AppSettings.Settings.Remove("EnableXPath");
                if (value)
                {
                    config.AppSettings.Settings.Add("EnableXPath", "true");
                }
                else
                {
                    config.AppSettings.Settings.Add("EnableXPath", "false");
                }
                config.Save(ConfigurationSaveMode.Minimal);

                SetProperty(value);
            }
        }

        public AutomationType SelectedAutomationType
        {
            get { return GetProperty<AutomationType>(); }
            private set { SetProperty(value); }
        }

        public ObservableCollection<ElementViewModel> Elements { get; private set; }

        public ICommand StartNewInstanceCommand { get; private set; }

        public ICommand StartNewInstanceWithVersionSelectionCommand { get; private set; }

        public ICommand CaptureSelectedItemCommand { get; private set; }

        public ICommand RefreshCommand { get; private set; }

        public ObservableCollection<DetailGroupViewModel> SelectedItemDetails => SelectedItemInTree?.ItemDetails;

        public ElementViewModel SelectedItemInTree
        {
            get { return GetProperty<ElementViewModel>(); }
            private set { SetProperty(value); }
        }

        public void Initialize(AutomationType selectedAutomationType)
        {
            SelectedAutomationType = selectedAutomationType;
            IsInitialized = true;

            _automation = selectedAutomationType == AutomationType.UIA2 ? (AutomationBase)new UIA2Automation() : new UIA3Automation();
            _rootElement = _automation.GetDesktop();
            var desktopViewModel = new ElementViewModel(_rootElement);
            desktopViewModel.SelectionChanged += DesktopViewModel_SelectionChanged;
            desktopViewModel.LoadChildren(false);
            Elements.Add(desktopViewModel);
            Elements[0].IsExpanded = true;

            // Initialize TreeWalker
            _treeWalker = _automation.TreeWalkerFactory.GetControlViewWalker();

            // Initialize hover
            _hoverMode = new HoverMode(_automation);
            _hoverMode.ElementHovered += ElementToSelectChanged;

            // Initialize focus tracking
            _focusTrackingMode = new FocusTrackingMode(_automation);
            _focusTrackingMode.ElementFocused += ElementToSelectChanged;

            // Set modes from config file
            var enableHoverMode = ConfigurationManager.AppSettings["EnableHoverMode"];
            var enableFocusTrackingMode = ConfigurationManager.AppSettings["EnableFocusTrackingMode"];
            var enableXPath = ConfigurationManager.AppSettings["EnableXPath"];

            EnableHoverMode = enableHoverMode == "true";
            EnableFocusTrackingMode = enableFocusTrackingMode == "true";
            EnableXPath = enableXPath == "true";
        }

        private void ElementToSelectChanged(AutomationElement obj)
        {
            // Build a stack from the root to the hovered item
            var pathToRoot = new Stack<AutomationElement>();
            while (obj != null)
            {
                // Break on circular relationship (should not happen?)
                if (pathToRoot.Contains(obj) || obj.Equals(_rootElement)) { break; }

                pathToRoot.Push(obj);
                try
                {
                    obj = _treeWalker.GetParent(obj);
                }
                catch (Exception ex)
                {
                    // TODO: Log
                    Console.WriteLine($"Exception: {ex.Message}");
                }
            }

            // Expand the root element if needed
            if (!Elements[0].IsExpanded)
            {
                Elements[0].IsExpanded = true;
                System.Threading.Thread.Sleep(1000);
            }

            var elementVm = Elements[0];
            while (pathToRoot.Count > 0)
            {
                var elementOnPath = pathToRoot.Pop();
                var nextElementVm = FindElement(elementVm, elementOnPath);
                if (nextElementVm == null)
                {
                    // Could not find next element, try reloading the parent
                    elementVm.LoadChildren(true);
                    // Now search again
                    nextElementVm = FindElement(elementVm, elementOnPath);
                    if (nextElementVm == null)
                    {
                        // The next element is still not found, exit the loop
                        Console.WriteLine("Could not find the next element!");
                        break;
                    }
                }
                elementVm = nextElementVm;
                if (!elementVm.IsExpanded)
                {
                    elementVm.IsExpanded = true;
                }
            }
            // Select the last element
            elementVm.IsSelected = true;
        }

        private ElementViewModel FindElement(ElementViewModel parent, AutomationElement element)
        {
            return parent.Children.FirstOrDefault(child => child.AutomationElement.Equals(element));
        }

        private void DesktopViewModel_SelectionChanged(ElementViewModel obj)
        {
            SelectedItemInTree = obj;
            OnPropertyChanged(() => SelectedItemDetails);
        }

        private void RefreshTree()
        {
            Elements.Clear();
            Initialize(SelectedAutomationType);
        }
    }
}
