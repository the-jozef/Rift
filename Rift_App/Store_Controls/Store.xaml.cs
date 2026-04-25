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
using Rift_App.Models;
using Rift_App.ViewModels;

namespace Rift_App.Store_Controls
{
    public partial class Store : UserControl
    {
        private readonly StoreViewModel _viewModel;

        public Store()
        {
            InitializeComponent();

            _viewModel = new StoreViewModel();
            DataContext = _viewModel;

            // When game clicked → open StoreGamePage
            // Keď klikne na hru → otvorí StoreGamePage
            _viewModel.OnGameSelected += HandleGameSelected;

            // Load store data when page opens
            Loaded += async (s, e) =>
            {
                try { await _viewModel.LoadStoreCommand.ExecuteAsync(null); }
                catch { }
            };
        }

        // ─── GAME SELECTED ────────────────────────────────────────────────

        private void HandleGameSelected(GameModel game)
        {
            try
            {
                // Navigate to game detail page
                // Navigujeme na stránku detailu hry
                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.ShowGamePage(game);
            }
            catch { }
        }

        // ─── SHOW MORE BUTTONS ────────────────────────────────────────────
        // Called from XAML buttons

        private async void ShowMoreNewTrending_Click(object sender, RoutedEventArgs e)
        {
            try { await _viewModel.ShowMoreNewTrendingCommand.ExecuteAsync(null); }
            catch { }
        }

        private async void ShowMoreTopSellers_Click(object sender, RoutedEventArgs e)
        {
            try { await _viewModel.ShowMoreTopSellersCommand.ExecuteAsync(null); }
            catch { }
        }

        private async void ShowMoreSpecials_Click(object sender, RoutedEventArgs e)
        {
            try { await _viewModel.ShowMoreSpecialsCommand.ExecuteAsync(null); }
            catch { }
        }

        // ─── GAME CLICK ───────────────────────────────────────────────────

        private void GameItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.DataContext is GameModel game)
                    _viewModel.SelectGameCommand.Execute(game);
            }
            catch { }
        }
    }
}