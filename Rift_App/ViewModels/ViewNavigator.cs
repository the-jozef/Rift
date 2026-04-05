using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Rift_App.Login_Register;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rift_App.ViewModels
{
    public partial class Navigator : ObservableObject
    {
        [ObservableProperty]
        private object currentView;

        public Navigator()
        {
            CurrentView = new Login();
        }

        [RelayCommand]
        public void ShowLogin()
        {
            CurrentView = new Login();
        }

        [RelayCommand]
        public void ShowRegister()
        {
            CurrentView = new Register(); // tvoj Register UserControl
        }
    }
}
