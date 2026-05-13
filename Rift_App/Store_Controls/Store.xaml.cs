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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Rift_App.Account
{
    public partial class Account : UserControl
    {
        private readonly AccountViewModel _vm = new AccountViewModel();

        public Account()
        {
            InitializeComponent();
            DataContext = _vm;
            Loaded += async (_, _) => await _vm.LoadAccountAsync();
        }
    }
}