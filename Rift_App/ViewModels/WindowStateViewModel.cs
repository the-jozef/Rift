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
using System.Windows.Input;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Rift_App.ViewModels
{
    public class WindowStateViewModel : ViewBase
    {
        public ICommand MinimizeCommand { get; }
        public ICommand MaximizeCommand { get; }
        public ICommand CloseCommand { get; }

        public WindowStateViewModel()
        {
            MinimizeCommand = new RelayCommand(
                () => App.Current.MainWindow.WindowState = System.Windows.WindowState.Minimized);


            MaximizeCommand = new RelayCommand(
                ()=>
            {
                var win = App.Current.MainWindow;
                win.WindowState = win.WindowState == System.Windows.WindowState.Maximized
                    ? System.Windows.WindowState.Normal
                    : System.Windows.WindowState.Maximized;
            });

            CloseCommand = new RelayCommand(
                () => App.Current.MainWindow.Close());
        }
    }
}
