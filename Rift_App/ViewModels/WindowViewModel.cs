using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rift_App.Authorization;
using Rift_App.Library;
using Rift_App.Models;
using Rift_App.Services;
using Rift_App.Store;
using Rift_App.StoreGamePage;
using Steamworks;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Rift_App.ViewModels
{
    public partial class WindowViewModel : ObservableObject
    {
        // ─── WINDOW STATE ─────────────────────────────────────────────────
        public WindowStateViewModel WindowState { get; } = new();

        // ─── CURRENT VIEW ─────────────────────────────────────────────────
        [ObservableProperty] private object _currentView = null!;

        // ─── SEARCH BAR ───────────────────────────────────────────────────
        [ObservableProperty] private bool _showSearchBar = true;

        // ─── PLAYER INFO ─────────────────────────────────────────────────
        // IMPORTANT: must be [ObservableProperty] — NOT a plain get => SessionManager.X
        // because plain computed properties never notify the UI when the session changes.
        [ObservableProperty] private string _username = string.Empty;
        [ObservableProperty] private string _avatarUrl = string.Empty;

        // ─── STEAM TIMER ──────────────────────────────────────────────────
        private readonly DispatcherTimer _steamTimer;

        // ─── CONSTRUCTOR ──────────────────────────────────────────────────
        public WindowViewModel()
        {
            _steamTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };

            // Subscribe so name/avatar update on every account switch
            SessionManager.OnSessionReady += OnSessionReady;

            InitializeSteam();
            ShowStore();
        }

        // ─── SESSION CHANGE ───────────────────────────────────────────────
        private void OnSessionReady()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Username = SessionManager.Username;
                AvatarUrl = SessionManager.AvatarUrl;
            });
        }

        public void ApplySession()
        {
            Username = SessionManager.Username;
            AvatarUrl = SessionManager.AvatarUrl;
        }

        // ─── STEAM ────────────────────────────────────────────────────────
        private void InitializeSteam()
        {
            _steamTimer.Tick += (_, _) =>
            {
                if (SteamworksService.IsInitialized)
                    SteamAPI.RunCallbacks();
            };
            _steamTimer.Start();

            SteamCallbackService.Register();
            _ = LastPlayedCacheService.InitializeAsync();
        }

        // Call from App.xaml.cs OnExit
        public void Cleanup()
        {
            SessionManager.OnSessionReady -= OnSessionReady;
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
        public void ShowGamePage(GameModel game)
        {
            var page = new StoreGamePage.GamePage();
            page.LoadGame(game);
            CurrentView = page;
            ShowSearchBar = false;
        }

        [RelayCommand]
        public void ShowSteam() => OpenSteamUrl("steam://store");

        [RelayCommand]
        public void ShowSteamFriends() => OpenSteamUrl("steam://open/friends");

        [RelayCommand]
        public void ShowSteamAccount() => OpenSteamUrl($"steam://url/SteamIDPage/{SessionManager.SteamId64}");

        // ─── SWITCH ACCOUNT ───────────────────────────────────────────────
        [RelayCommand]
        public void SwitchAccount()
        {
            SessionManager.Clear();
            ViewNavigator.Instance?.SwitchToAuth();
        }

        // ─── LAST LOCATION ────────────────────────────────────────────────
        public void NavigateToLastLocation(string location)
        {
            // Apply session FIRST — top bar shows correct user before any view loads
            ApplySession();

            switch (location)
            {
                case "Library": ShowLibrary(); break;
                case "Wishlist": ShowWishlist(); break;
                case "Account": ShowAccount(); break;
                default: ShowStore(); break;
            }
        }

        // ─── HELPER ───────────────────────────────────────────────────────
        private static void OpenSteamUrl(string url)
        {
            if (!SteamworksService.IsSteamInstalled())
            {
                MessageBox.Show("Steam is not installed on this computer.",
                    "Steam Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                { FileName = url, UseShellExecute = true });
        }
    }
}