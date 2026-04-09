using Rift_App.ViewModels;
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
        
           
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ViewModel.SetupPassword = PasswordBox.Password;
        }
    }
}
