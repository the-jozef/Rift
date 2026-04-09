using Rift_App.Database;
using Rift_App.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Rift_App.Login_Register
{
    public partial class Login : UserControl
    {
        private LoginViewModel ViewModel => (LoginViewModel)DataContext;

        public Login()
        {
            InitializeComponent();
            DataContext = new LoginViewModel();

            // Spustí sa pri otvorení login okna
            this.Loaded += Login_Loaded;
        }

        private void Login_Loaded(object sender, RoutedEventArgs e)
        {
            var auth = new AuthService();
            var remembered = auth.GetRememberedCredentials();

            if (remembered.HasValue)
            {
                var result = MessageBox.Show(
                    $"Chcete sa automaticky prihlásiť ako **{remembered.Value.Username}**?\n\n" +
                    "Polia Account name a Password sa vyplnia automaticky.",
                    "Automatické prihlásenie",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    UsernameBox.Text = remembered.Value.Username;
                    PasswordBox.Password = remembered.Value.Password;
                }
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
                ViewModel.SetupPassword = PasswordBox.Password;
        }
    }
}