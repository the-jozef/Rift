using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rift_App.Login_Register;
using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Rift_App.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        [ObservableProperty] private object currentView;
        [ObservableProperty] private object accountSelectionView;

        private Login_loading _preloadedLoading;
        private Window _hiddenWindow;
        private bool _loadingReady = false;
        private bool _loadingRequested = false;

        public RegisterViewModel()
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
        private void AccountSelection() => CurrentView = null;

        [RelayCommand]
        private void Loading()
        {
                CurrentView = new Login_loading(); // video už hrá → okamžite
        }
    }
}