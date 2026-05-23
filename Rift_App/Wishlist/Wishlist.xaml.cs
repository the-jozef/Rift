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

            _viewModel.OnGameSelected += game =>
                (Application.Current.MainWindow as MainWindow)?.ViewModel.ShowGamePage(game);

            Loaded += async (_, _) =>
            {
                if (SessionManager.IsLoggedIn)
                    await _viewModel.LoadWishlistCommand.ExecuteAsync(null);
            };

            IsVisibleChanged += async (_, e) =>
            {
                if ((bool)e.NewValue && SessionManager.IsLoggedIn)
                    await _viewModel.RefreshIfStaleCommand.ExecuteAsync(null);
            };

            SessionManager.OnSessionReady += async () =>
            {
                if (_viewModel.Games.Count == 0)
                    await _viewModel.LoadWishlistCommand.ExecuteAsync(null);
            };
        }
    }
}