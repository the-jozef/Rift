using Rift_App.Services;
using Rift_App.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Rift_App
{
    public partial class AuthWindow : Window
    {
        public AuthViewModel ViewModel => (AuthViewModel)DataContext;

        public AuthWindow()
        {
            InitializeComponent();
            DataContext = new AuthViewModel();
            Closing += (s, e) =>
            {
                e.Cancel = true;
                SteamAuthService.Cancel();
                Hide();
            };
        }

        public void Reinitialize()
        {
            DataContext = new AuthViewModel();
        }
    }
}