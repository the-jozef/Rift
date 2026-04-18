using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rift_App.Login_Register;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rift_App.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        [ObservableProperty]
        private object currentView;

        public RegisterViewModel()
        {
            CurrentView = new Login();
        }

        [RelayCommand]
        public void ShowLogin()
        {
            CurrentView = new Login();
        }

        [RelayCommand]
        public void ShowSteamConnection()
        {
            CurrentView = new SteamConnection();
        }

        [RelayCommand]
        private void ShowRegister()
        {
            CurrentView = new Register();
        }
       

     


    }
}
