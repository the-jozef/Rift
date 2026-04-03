using Rift_App.ViewModels;
using System.Windows;
using System.Windows.Controls;
namespace Rift_App.Login_Register
{
    public partial class Register : UserControl
    {
        public Register()
        {
            InitializeComponent();
            DataContext = new LoginViewModel();
        }
        private void SetupPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel vm)
                vm.SetupPassword = ((PasswordBox)sender).Password;
        }
    }
}