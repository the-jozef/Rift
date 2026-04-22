using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rift_App.Login_Register;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Rift_App.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        [ObservableProperty]
        private object currentView;

        public RegisterViewModel()
        {
            CurrentView = new AccountSelection();
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
        [RelayCommand]
        private void AccountSelection()
        {
            CurrentView = new AccountSelection();
        }
        [RelayCommand]
        private void Loading()
        {
            //CurrentView = new Login_loading();
            
            var loadingView = new Login_loading { Opacity = 0 };

            loadingView.VideoReady += (s, e) =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                    loadingView.BeginAnimation(UIElement.OpacityProperty, fadeIn); 
                    CurrentView = loadingView;
                });
            };     
        }
    }   
}