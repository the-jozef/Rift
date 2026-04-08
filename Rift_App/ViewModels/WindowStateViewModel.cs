using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Rift_App.Login_Register;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Rift_App.ViewModels
{
    public partial class WindowStateViewModel : ObservableObject
    {
        
        [RelayCommand]
        private static void MaximizeWindow()
        {
            if (App.Current.MainWindow.WindowState == System.Windows.WindowState.Maximized)
            {
                App.Current.MainWindow.WindowState = System.Windows.WindowState.Normal;
            }
            else
            {
                App.Current.MainWindow.WindowState = System.Windows.WindowState.Maximized;
            }
        }


        [RelayCommand]
        private static void MinimizeWindow()
        {
            App.Current.MainWindow.WindowState = System.Windows.WindowState.Minimized;
        }
        [RelayCommand]
        private static void CloseWindow()
        {             
            App.Current.MainWindow.Close();
     
        }
    }
}
