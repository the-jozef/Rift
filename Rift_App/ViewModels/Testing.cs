using Rift_App.Login_Register;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Rift_App.ViewModels
{
    public partial class Testing : ObservableObject
    {
        private Login _loginView = new Login();
        private Register _registerView = new Register();

        [ObservableProperty]
        private object _currentView;


        public Testing()
        {
            // Nastavíme počiatočné zobrazenie
            _currentView = _loginView;
        }


        



        [RelayCommand]
        public void Register_Test()
        {
            CurrentView = _registerView;
        }






    }
    
}
