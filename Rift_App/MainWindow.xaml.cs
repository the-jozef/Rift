using CommunityToolkit.Mvvm.Input;
using Rift_App.Account;
using Rift_App.Authorization;
using Rift_App.Models;
using Rift_App.Services;
using Rift_App.Testing;
using Rift_App.ViewModels;
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
namespace Rift_App
{
    public partial class MainWindow : Window
    {
        public WindowViewModel ViewModel { get; } = new WindowViewModel();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = ViewModel;
            Closing += (s, e) => { e.Cancel = true; Hide(); };
        }
    }
}