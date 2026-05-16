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
using Steamworks;
using System;
using System.Windows;
using System.Windows.Threading;

namespace Rift_App.ViewModels
{
    public partial class WindowViewModel : ObservableObject
    {
        // ─── WINDOW STATE ─────────────────────────────────────────────────
        public WindowStateViewModel WindowState { get; } = new();

        // ─── CURRENT VIEW ─────────────────────────────────────────────────
        [ObservableProperty]
        private object _currentView = null!;

        // ─── SEARCH BAR ───────────────────────────────────────────────────
        [ObservableProperty]
        private bool _showSearchBar = true;

        // ─── PLAYER INFO ──────────────────────────────────────────────────
        public string Username => SessionManager.Username;
        public string AvatarUrl => SessionManager.AvatarUrl;

        // ─── STEAM TIMER ──────────────────────────────────────────────────
        private readonly DispatcherTimer _steamTimer;

        // ─── CONSTRUCTOR ──────────────────────────────────────────────────

        public WindowViewModel()
        {
            _steamTimer = BuildSteamTimer();
            InitializeSteam();
            ShowStore();
        }

        // ─── STEAM ────────────────────────────────────────────────────────

        private static DispatcherTimer BuildSteamTimer() => new()
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };

        private void InitializeSteam()
        {
            // RunCallbacks must be called regularly for ALL Steamworks events to fire
            _steamTimer.Tick += (_, _) =>
            {
                if (SteamworksService.IsInitialized)
                    SteamAPI.RunCallbacks();
            };
            _steamTimer.Start();

            // FileSystemWatcher on localconfig.vdf + Steamworks achievement callbacks
            SteamCallbackService.Register();

            // Pre-load LastPlayed cache (reads localconfig.vdf once)
            _ = LastPlayedCacheService.InitializeAsync();
        }

        // Called from App.xaml.cs OnExit — no need to touch MainWindow.xaml.cs
        public void Cleanup()
        {
            _steamTimer.Stop();
            SteamCallbackService.Unregister();
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