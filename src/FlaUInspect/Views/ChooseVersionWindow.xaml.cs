﻿using System.Windows;
using FlaUI.Core;

namespace FlaUInspect.Views;

/// <summary>
/// Interaction logic for ChooseVersionWindow.xaml
/// </summary>
public partial class ChooseVersionWindow {
    public ChooseVersionWindow() {
        InitializeComponent();
    }

    public AutomationType SelectedAutomationType { get; private set; }

    private void Uia2ButtonClick(object sender, RoutedEventArgs e) {
        SelectedAutomationType = AutomationType.UIA2;
        DialogResult = true;
    }

    private void Uia3ButtonClick(object sender, RoutedEventArgs e) {
        SelectedAutomationType = AutomationType.UIA3;
        DialogResult = true;
    }
}