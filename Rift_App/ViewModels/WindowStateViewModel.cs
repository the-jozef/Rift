using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Rift_App.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Windows;

namespace Rift_App.ViewModels
{
    public partial class WindowStateViewModel
    {
        private static Window GetWindow() =>
            Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
            ?? Application.Current.MainWindow;

        [RelayCommand]
        private void Minimize() => GetWindow().WindowState = WindowState.Minimized;

        [RelayCommand]
        private void Maximize()
        {
            var win = GetWindow();
            win.WindowState = win.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        [RelayCommand]
        private void Close()
        {
            App.Current.Shutdown();
        }
    }
}