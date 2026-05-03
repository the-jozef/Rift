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
using Rift_App.Services;

namespace Rift_App.Store
{
    public partial class Store : UserControl
    {
        private readonly StoreViewModel _viewModel = new();

        public Store()
        {
            InitializeComponent();
            DataContext = _viewModel;

            // Napoj event — klik na hru načíta plné detaily a prepne na GamePage
            // Wire event — game click loads full details and switches to GamePage
            _viewModel.OnGameSelected += OnGameSelected;

            Loaded += async (s, e) => await _viewModel.LoadStoreCommand.ExecuteAsync(null);
        }

        private async void OnGameSelected(GameModel game)
        {
            var fullGame = await ApiService.GetGameDetailsAsync(game.AppId);
            var gameToShow = fullGame ?? game;

            if (Application.Current.MainWindow is MainWindow main)
                main.ViewModel.ShowGamePageCommand.Execute(gameToShow);
        }
    }
}