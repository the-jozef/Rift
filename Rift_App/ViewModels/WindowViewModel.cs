using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rift_App.Authorization;
using Rift_App.Languages;
using Rift_App.Library;
using Rift_App.Models;
using Rift_App.Services;
using Rift_App.Store;
using Rift_App.StoreGamePage;
using Steamworks;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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

        // ─── VIEW ─────────────────────────────────────────────────
        [ObservableProperty] private object _currentView = null!;

        private Library.Library? _libraryView;
        private Wishlist.Wishlist? _wishlistView;
        private Account.Account? _accountView;

        // ─── SEARCH BAR ───────────────────────────────────────────────────
        [ObservableProperty] private bool _showSearchBar = true;

        // ─── PLAYER INFO ─────────────────────────────────────────────────
        // IMPORTANT: must be [ObservableProperty] — NOT a plain get => SessionManager.X
        // because plain computed properties never notify the UI when the session changes.
        [ObservableProperty] private string _username = string.Empty;
        [ObservableProperty] private string _avatarUrl = string.Empty;

        // ─── STEAM TIMER ──────────────────────────────────────────────────
        private readonly DispatcherTimer _steamTimer;

        // ─── LANGUAGE ──────────────────────────────────────────────────
        [ObservableProperty]
        private string _currentLanguage = LanguageService.CurrentLanguage;

        // ─── CONSTRUCTOR ──────────────────────────────────────────────────
        public WindowViewModel()
        {
            _steamTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };

            // Subscribe so name/avatar update on every account switch
            SessionManager.OnSessionReady += OnSessionReady;

            LanguageService.LanguageChanged += () =>
            {
                CurrentLanguage = LanguageService.CurrentLanguage;
            };

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
            _libraryView ??= new Library.Library();
            CurrentView = _libraryView;
            ShowSearchBar = false;
        }

        [RelayCommand]
        public void ShowWishlist()
        {
            _ = ApiService.SaveSessionAsync("Wishlist");
            _wishlistView ??= new Wishlist.Wishlist();
            CurrentView = _wishlistView;
            ShowSearchBar = false;
        }

        [RelayCommand]
        public void ShowAccount()
        {
            _ = ApiService.SaveSessionAsync("Account");
            _accountView ??= new Account.Account();
            CurrentView = _accountView;
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
        public void SwitchAccount()
        {
            _libraryView = null;
            _wishlistView = null;
            _accountView = null;
            SessionManager.Clear();
            ViewNavigator.Instance?.SwitchToAuth();
        }

        [RelayCommand]
        public void ShowSteam() => OpenSteamUrl("steam://store");

        [RelayCommand]
        public void ShowNotifications() => OpenSteamUrl($"steam://openurl/https://steamcommunity.com/profiles/{SessionManager.SteamId64}/notifications");

        [RelayCommand]
        public void ShowSteamFriends() => OpenSteamUrl("steam://open/friends");

        [RelayCommand]
        public void ShowSteamAccount() => OpenSteamUrl($"steam://url/SteamIDPage/{SessionManager.SteamId64}");

        [RelayCommand]
        private async Task SwitchLanguageAsync(string lang)
        {
            if (LanguageService.CurrentLanguage == lang) return;

            LanguageService.Switch(lang);

            _libraryView = null;
            _wishlistView = null;
            _accountView = null;

            ViewNavigator.Instance?.ShowLoading();
        }

        [RelayCommand]
        private void OpenDocumentSK()
        {
            try
            {
                var sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Documents", "RIFT_SK.docx");

                if (!File.Exists(sourcePath))
                {
                    MessageBox.Show(
              L.Get("msg_document_not_found"),
              L.Get("msg_document_not_found_title"),
              MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Copy to Temp as read-only — user cannot delete from original
                var tempPath = Path.Combine(Path.GetTempPath(), "rift_manual_sk.docx");

                // if exists, remove read-only attribute so it can be overwritten
                if (File.Exists(tempPath))
                    File.SetAttributes(tempPath, FileAttributes.Normal);

                File.Copy(sourcePath, tempPath, overwrite: true);
                File.SetAttributes(tempPath, FileAttributes.ReadOnly);

                // Open in Word / default program
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Document] Open error: {ex.Message}");

                MessageBox.Show(
         L.Get("msg_document_open_failed"),
         L.Get("msg_document_open_failed_title"),
         MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void OpenDocumentEN()
        {
            try
            {
                //Path
                var sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Documents", "RIFT_EN.docx");

                if (!File.Exists(sourcePath))
                {
                    MessageBox.Show(
      L.Get("msg_document_not_found"),
      L.Get("msg_document_not_found_title"),
      MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Copy to Temp as read-only — user cannot delete from original
                var tempPath = Path.Combine(Path.GetTempPath(), "rift_manual_en.docx");

                // if exists, remove read-only attribute so it can be overwritten
                if (File.Exists(tempPath))
                    File.SetAttributes(tempPath, FileAttributes.Normal);

                File.Copy(sourcePath, tempPath, overwrite: true);

                // Set read-only attribute
                File.SetAttributes(tempPath, FileAttributes.ReadOnly);

                // Open in Word / default program
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Document] Open error: {ex.Message}");

                MessageBox.Show(
        L.Get("msg_document_open_failed"),
        L.Get("msg_document_open_failed_title"),
        MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                MessageBox.Show(
     L.Get("msg_steam_not_found"),
     L.Get("msg_steam_not_found_title"),
     MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                { FileName = url, UseShellExecute = true });
        }
    }
}