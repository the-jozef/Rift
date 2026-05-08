using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rift_App.Models;
using Rift_App.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Windows;

namespace Rift_App.ViewModels
{
    public partial class LibraryViewModel : ObservableObject
    {
        public ObservableCollection<GameModel> Games { get; } = new();
        public ObservableCollection<GameModel> FilteredGames { get; } = new();

        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private int _totalGames = 0;
        [ObservableProperty] private int _installedCount = 0;

        // Fired when user clicks a game — Library.xaml.cs forwards to GameDetailPanel
        public event Action<GameModel>? OnGameSelected;

        // ─── SELECT GAME ──────────────────────────────────────────────────

        [RelayCommand]
        private void SelectGame(GameModel game)
        {
            if (game != null) OnGameSelected?.Invoke(game);
        }

        // ─── LOAD ─────────────────────────────────────────────────────────

        [RelayCommand]
        public async Task LoadLibraryAsync()
        {
            IsLoading = true;
            Games.Clear();
            FilteredGames.Clear();

            try
            {
                // 1. Load from disk cache first — instant startup
                var cached = await LibraryCacheService.LoadAsync();

                if (cached != null && cached.Count > 0)
                {
                    RestoreIconPaths(cached);
                    CheckInstallStatus(cached);
                    PopulateGames(cached);
                    IsLoading = false;

                    // Background sync to check for new/removed games
                    _ = SyncInBackgroundAsync();
                    return;
                }

                // 2. First run — get games directly from local Steam install
                var games = SteamInstallService.GetAllGames();
                if (games.Count == 0)
                {
                    Debug.WriteLine("[Library] No games found from SteamInstallService");
                    return;
                }

                Debug.WriteLine($"[Library] Found {games.Count} games from Steam");

                var sorted = games.OrderByDescending(g => g.PlaytimeMinutes).ToList();
                CheckInstallStatus(sorted);

                // Download icons to disk
                await LibraryCacheService.DownloadAllIconsAsync(sorted);
                await LibraryCacheService.SaveAsync(sorted);

                PopulateGames(sorted);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Library] Load error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                await ApiService.SaveSessionAsync("Library");
            }
        }

        // ─── BACKGROUND SYNC ──────────────────────────────────────────────
        // Runs after UI is shown — adds new games, removes deleted ones

        private async Task SyncInBackgroundAsync()
        {
            try
            {
                await Task.Delay(3000);

                // Get fresh list from local Steam install
                var fresh = SteamInstallService.GetAllGames();
                if (fresh.Count == 0) return;

                var cached = Games.ToList();
                var (synced, changed) = await LibraryCacheService.SyncAsync(cached, fresh);
                if (!changed) return;

                RestoreIconPaths(synced);
                CheckInstallStatus(synced);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Games.Clear();
                    FilteredGames.Clear();
                    PopulateGames(synced.OrderByDescending(g => g.PlaytimeMinutes).ToList());
                });

                await LibraryCacheService.SaveAsync(synced);
                Debug.WriteLine("[Library] Sync complete.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Library] Sync error: {ex.Message}");
            }
        }

        // ─── INSTALL STATUS ───────────────────────────────────────────────
        // Uses Steamworks if initialized, falls back to .acf file reader

        private static void CheckInstallStatus(List<GameModel> games)
        {
            foreach (var game in games)
            {
                InstallInfo info;

                if (SteamworksService.IsInitialized)
                    info = SteamworksService.GetInstallInfo(game.AppId);
                else
                    info = SteamInstallService.GetInfo(game.AppId);

                game.IsInstalled = info.IsInstalled;
            }
        }

        // ─── HELPERS ──────────────────────────────────────────────────────

        private static void RestoreIconPaths(List<GameModel> games)
        {
            foreach (var game in games)
            {
                var path = LibraryCacheService.GetIconPath(game.AppId);
                game.IconPath = File.Exists(path) ? path : null;
            }
        }

        private void PopulateGames(List<GameModel> games)
        {
            // Installed first, then uninstalled — both sorted by playtime
            var sorted = games
                .OrderByDescending(g => g.IsInstalled)
                .ThenByDescending(g => g.PlaytimeMinutes)
                .ToList();

            foreach (var game in sorted)
            {
                Games.Add(game);
                FilteredGames.Add(game);
            }

            TotalGames = Games.Count;
            InstalledCount = Games.Count(g => g.IsInstalled);
        }

        // ─── SEARCH ───────────────────────────────────────────────────────

        partial void OnSearchTextChanged(string value)
        {
            FilteredGames.Clear();
            var source = string.IsNullOrWhiteSpace(value)
                ? Games
                : Games.Where(g => g.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
            foreach (var game in source) FilteredGames.Add(game);
        }
    }
}