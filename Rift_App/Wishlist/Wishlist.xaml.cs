using Rift_App.Models;
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
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace Rift_App.Wishlist
{
    public partial class Wishlist : UserControl
    {
        private readonly WishlistViewModel _viewModel;

        public Wishlist()
        {
            InitializeComponent();

            _viewModel = new WishlistViewModel();
            DataContext = _viewModel;

            // When game clicked → open store game page
            _viewModel.OnGameSelected += HandleGameSelected;

            Loaded += async (s, e) =>
            {
                try
                {
                    // Guest mode — show message, don't load
                    // Hosť — zobrazíme správu, nenačítame nič
                    if (!SessionManager.IsLoggedIn)
                    {
                        ShowGuestMessage();
                        return;
                    }

                    await _viewModel.LoadWishlistCommand.ExecuteAsync(null);
                }
                catch { }
            };
        }

        // ─── GAME SELECTED ────────────────────────────────────────────────

        private void HandleGameSelected(GameModel game)
        {
            try
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.ShowGamePage(game);
            }
            catch { }
        }

        // ─── GUEST MESSAGE ────────────────────────────────────────────────

        private void ShowGuestMessage()
        {
            try
            {
               // if (GuestPanel != null) GuestPanel.Visibility = Visibility.Visible;
                //if (GamesPanel != null) GamesPanel.Visibility = Visibility.Collapsed;
            }
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