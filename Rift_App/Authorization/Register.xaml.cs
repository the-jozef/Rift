using Rift_App.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
 
namespace Rift_App.Authorization
{
    public partial class Register : UserControl
    {
        private readonly RegisterViewModel _viewModel;

        public Register()
        {
            InitializeComponent();

            _viewModel = new RegisterViewModel();
            DataContext = _viewModel;

            // Register success → Loading screen
            _viewModel.OnRegisterSuccess += () =>
            {
                try { ViewNavigator.Instance?.ShowLoading(); }
                catch { }
            };

            // Back to Login
            _viewModel.OnGoToLogin += () =>
            {
                try { ViewNavigator.Instance?.ShowLogin(); }
                catch { }
            };
        }

        // ─── PRE-FILL — called after SteamConnection succeeds ────────────
        // Predvyplnenie — volané po úspešnom Steam pripojení

        public void PreFill(string steamId, string steamName)
        {
            try { _viewModel.PreFillSteam(steamId, steamName); }
            catch { }
        }

        // ─── PASSWORD BOXES ───────────────────────────────────────────────

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is PasswordBox pb)
                    _viewModel.Password = pb.Password;
            }
            catch { }
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is PasswordBox pb)
                    _viewModel.ConfirmPassword = pb.Password;
            }
            catch { }
        }
    }
}