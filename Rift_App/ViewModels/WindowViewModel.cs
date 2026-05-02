using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rift_App.Authorization;
using Rift_App.Library;
using Rift_App.Services;
using Rift_App.Store;
using Rift_App.StoreGamePage;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rift_App.ViewModels
{
    public partial class WindowViewModel : ObservableObject
    {
        // ─── USERNAME ──────────────────────────────────────────────────────
        public string Username => SessionManager.Username;

        // ─── WINDOW STATE ─────────────────────────────────────────────────
        public WindowStateViewModel WindowState { get; } = new();

        // ─── CURRENT VIEW ─────────────────────────────────────────────────
        [ObservableProperty]
        private object _currentView = null!;

        // ─── SEARCH BAR ───────────────────────────────────────────────────
        [ObservableProperty]
        private bool _showSearchBar = true;

        public WindowViewModel()
        {
            ShowStore();
        }

        // ─── NAVIGATION ───────────────────────────────────────────────────

        [RelayCommand]
        public void ShowStore()
        {
            _ = ApiService.SaveSessionAsync("Store");
            CurrentView = new Store.Store();
            ShowSearchBar = true;
        }

        [RelayCommand]
        public void ShowLibrary()
        {
            _ = ApiService.SaveSessionAsync("Library");
            CurrentView = new Library.Library();
            ShowSearchBar = false;
        }

        [RelayCommand]
        public void ShowWishlist()
        {
            _ = ApiService.SaveSessionAsync("Wishlist");
            CurrentView = new Wishlist.Wishlist();
            ShowSearchBar = false;
        }

        [RelayCommand]
        public void ShowAccount()
        {
            _ = ApiService.SaveSessionAsync("Account");
            CurrentView = new Account.Account();
            ShowSearchBar = false;
        }

        [RelayCommand]
        public void ShowGamePage(Models.GameModel game)
        {
            var page = new StoreGamePage.GamePage();
            page.LoadGame(game);
            CurrentView = page;
            ShowSearchBar = false;
        }

        // ─── SWITCH ACCOUNT ───────────────────────────────────────────────

        [RelayCommand]
        public void SwitchAccount()
        {
            SessionManager.Clear();
            ViewNavigator.Instance?.SwitchToAuth();
        }

        // ─── NAVIGATE TO LAST LOCATION ────────────────────────────────────

        public void NavigateToLastLocation(string location)
        {
            switch (location)
            {
                case "Library": ShowLibrary(); break;
                case "Wishlist": ShowWishlist(); break;
                case "Account": ShowAccount(); break;
                default: ShowStore(); break;
            }
        }
    }
}