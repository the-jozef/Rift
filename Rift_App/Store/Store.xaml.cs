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

            // Napoj event — ked user klikne na hru nacitaj plne detaily a prepni na GamePage
            // Wire event — when user clicks a game, load full details and navigate to GamePage
            _viewModel.OnGameSelected += OnGameSelected;

            Loaded += async (s, e) =>
            {
                await _viewModel.LoadStoreCommand.ExecuteAsync(null);
                MessageBox.Show($"Loaded: {_viewModel.NewTrending.Count} games", "Debug");
            };
        }

        // Nacita plne detaily hry zo Steam API a otvori GamePage
        // Loads full game details from Steam API and opens GamePage
        private async void OnGameSelected(GameModel game)
        {
            // Hra zo store listu ma iba zakladne info (meno, header, cena)
            // Game from store list only has basic info (name, header, price)
            // Donacitaj plne detaily — screenshoty, popis, zanre
            // Load full details — screenshots, description, genres
            var fullGame = await ApiService.GetGameDetailsAsync(game.AppId);

            // Ak API zlyhalo pouzi co mame — if API failed use what we have
            var gameToShow = fullGame ?? game;

            if (Application.Current.MainWindow is MainWindow main)
                main.ViewModel.ShowGamePageCommand.Execute(gameToShow);
        }
    }
}