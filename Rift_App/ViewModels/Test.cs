using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rift_App.Authorization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Rift_App.ViewModels
{
    public partial class Test : ObservableObject
    {
        [ObservableProperty] private object _currentView;
        [ObservableProperty] private object _accountSelectionView;
        public Test()
        {
            CurrentView = new AccountSelection();
        }
        [RelayCommand]
        public void ShowLogin() => CurrentView = new Login();

        [RelayCommand]
        public void ShowSteamConnection() => CurrentView = new SteamConnection();

        [RelayCommand]
        private void ShowRegister() => CurrentView = new Register();

        [RelayCommand]
        private void AccountSelection() => CurrentView = new AccountSelection();

    }
}
