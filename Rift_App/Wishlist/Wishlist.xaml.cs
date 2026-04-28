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
        private readonly WishlistViewModel _viewModel = new();

        public Wishlist()
        {
            InitializeComponent();
            DataContext = _viewModel;
            Loaded += Wishlist_Loaded;
        }

        private async void Wishlist_Loaded(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.IsLoggedIn)
            {
                ShowGuestMessage();
                return;
            }
            await _viewModel.LoadWishlistCommand.ExecuteAsync(null);
        }

        private void ShowGuestMessage()
        {
           // if (GuestPanel != null) GuestPanel.Visibility = Visibility.Visible;
           // if (GamesPanel != null) GamesPanel.Visibility = Visibility.Collapsed;
        }

        private void GameItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is GameModel game)
                _viewModel.SelectGameCommand.Execute(game);
        }
    }
}