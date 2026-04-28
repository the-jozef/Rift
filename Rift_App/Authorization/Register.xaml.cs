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
        public Register() => InitializeComponent();

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is AuthViewModel vm && sender is PasswordBox pb)
                vm.RegisterPassword = pb.Password;
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is AuthViewModel vm && sender is PasswordBox pb)
                vm.RegisterConfirmPassword = pb.Password;
        }
    }
}