using Rift_App.Models;
using Rift_App.Services;
using Rift_App.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Rift_App.Loading
{
    public partial class LoadingWindow : Window
    {
        private PlayerInfo? _playerInfo;
        private List<GameModel> _library = new();
        private List<GameModel> _wishlist = new();

        public LoadingWindow()
        {
            InitializeComponent();

            // Closing = hide, not exit
            this.Closing += (s, e) =>
            {
                e.Cancel = true;
                this.Hide();
            };

            // Start loading when window opens
            this.Loaded += async (s, e) =>
            {
                try { await LoadAllDataAsync(); }
                catch { NavigateToMainWindow(); }
            };
        }

        // ─── LOAD ALL DATA IN PARALLEL ────────────────────────────────────

        private async Task LoadAllDataAsync()
        {
            try
            {
                var steamId = SessionManager.SteamId64;

                // Guest mode — skip Steam data
                // Hosť — preskočíme Steam dáta
                if (string.IsNullOrEmpty(steamId))
                {
                    NavigateToMainWindow();
                    return;
                }

                // Load everything at once — načítame všetko naraz
                var playerTask = ApiService.GetPlayerInfoAsync(steamId);
                var libraryTask = ApiService.GetLibraryAsync(steamId);
                var wishlistTask = ApiService.GetWishlistAsync(steamId);

                await Task.WhenAll(playerTask, libraryTask, wishlistTask);

                _playerInfo = playerTask.Result;
                _library = libraryTask.Result;
                _wishlist = wishlistTask.Result;
            }
            catch
            {
                // Loading failed — still navigate with empty data
            }
            finally
            {
                NavigateToMainWindow();
            }
        }

        // ─── NAVIGATE TO MAIN WINDOW ──────────────────────────────────────

        private void NavigateToMainWindow()
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ViewNavigator.Instance?.ShowMainWindow(
                        _playerInfo,
                        _library,
                        _wishlist,
                        SessionManager.LastLocation
                    );
                });
            }
            catch
            {
                try
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        ViewNavigator.Instance?.ShowMainWindow(null, new(), new(), "Store"));
                }
                catch { }
            }
        }
    }
}