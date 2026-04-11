using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        private bool _isOnStep1 = true;

        [RelayCommand]
        private void SteamLogin()
        {
            IsOnStep1 = false; // prepne na Step 2
        }

        [RelayCommand]
        private void CancelSetup()
        {
            IsOnStep1 = true; // vráti na Step 1
        }
    }
}
