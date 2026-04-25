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
    public partial class Login : UserControl
    {
        private readonly LoginViewModel _viewModel;

        public Login()
        {
            InitializeComponent();

            _viewModel = new LoginViewModel();
            DataContext = _viewModel;

            // Login success → Loading screen
            _viewModel.OnLoginSuccess += () =>
            {
                try { ViewNavigator.Instance?.ShowLoading(); }
                catch { }
            };

            // Go to Register → SteamConnection first
            _viewModel.OnGoToRegister += () =>
            {
                try { ViewNavigator.Instance?.ShowSteamConnection(); }
                catch { }
            };

            // Back → AccountSelection
            _viewModel.OnGoBack += () =>
            {
                try { ViewNavigator.Instance?.ShowAccountSelection(); }
                catch { }
            };
        }

        // ─── PASSWORD BOX ─────────────────────────────────────────────────
        // PasswordBox nepodporuje binding — riešime ručne (manually)

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is PasswordBox pb)
                    _viewModel.Password = pb.Password;
            }
            catch { }
        }
    }
}