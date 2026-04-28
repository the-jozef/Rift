using Rift_App.Models;
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
using Rift_App.ViewModels;

namespace Rift_App.Store
{
    public partial class Store : UserControl
    {
        private readonly StoreViewModel _viewModel = new();

        public Store()
        {
            InitializeComponent();
            DataContext = _viewModel;
            Loaded += async (s, e) => await _viewModel.LoadStoreCommand.ExecuteAsync(null);
        }

        private void GameItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is GameModel game)
                _viewModel.SelectGameCommand.Execute(game);
        }
    }
}