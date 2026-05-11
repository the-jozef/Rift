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
                var cached = await LibraryCacheService.LoadAsync();
                if (cached != null && cached.Count > 0)
                {
                    RestoreIconPaths(cached);
                    CheckInstallStatus(cached);
                    PopulateGames(cached);
                    IsLoading = false;
                    _ = SyncInBackgroundAsync();
                    return;
                }

                var steamId = SessionManager.SteamId64;

                // 1. API — vlastnené hry vrátane free, tools, soundtracks
                var apiGames = await ApiService.GetLibraryAsync(steamId);
                Debug.WriteLine($"[Library] API: {apiGames.Count} hier");

                // 2. Lokálny sken — nainštalované vrátane demo, prologue, beta
                var localGames = SteamInstallService.GetAllGames();
                Debug.WriteLine($"[Library] Lokálne: {localGames.Count} hier");

                // 3. Spoj — lokálne hry ktoré nie sú v API (demo, prologue...)
                var apiIds = apiGames.Select(g => g.AppId).ToHashSet();
                var localOnly = localGames.Where(g => !apiIds.Contains(g.AppId)).ToList();
                Debug.WriteLine($"[Library] Lokálne navyše: {localOnly.Count} hier");

                var allGames = apiGames.Concat(localOnly).ToList();

                // 4. Playtime
                var playtime = SteamworksService.GetPlaytimeMinutes();
                foreach (var game in allGames)
                {
                    if (playtime.TryGetValue(game.AppId, out int minutes))
                        game.PlaytimeMinutes = minutes;
                }

                Debug.WriteLine($"[Library] Celkovo: {allGames.Count} hier");

                CheckInstallStatus(allGames);
                await LibraryCacheService.DownloadAllIconsAsync(allGames);
                await LibraryCacheService.SaveAsync(allGames);
                PopulateGames(allGames);
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

                var steamId = SessionManager.SteamId64;

                // Vždy API — nie lokálny sken
                var fresh = await ApiService.GetLibraryAsync(steamId);
                if (fresh.Count == 0) return;

                // Playtime update
                var playtime = SteamworksService.GetPlaytimeMinutes();
                foreach (var game in fresh)
                {
                    if (playtime.TryGetValue(game.AppId, out int minutes))
                        game.PlaytimeMinutes = minutes;
                }

                var cached = Games.ToList();
                var (synced, changed) = await LibraryCacheService.SyncAsync(cached, fresh);
                if (!changed) return;

                RestoreIconPaths(synced);
                CheckInstallStatus(synced);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Games.Clear();
                    FilteredGames.Clear();
                    PopulateGames(synced);
                });

                await LibraryCacheService.SaveAsync(synced);
                Debug.WriteLine("[Library] Sync hotový.");
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
            var sorted = games
                .OrderByDescending(g => g.IsInstalled)          // nainštalované prvé
                .ThenByDescending(g => g.PlaytimeMinutes)        // potom podľa playtime
                .ThenBy(g => g.Name)                             // abecedne zvyšok
                .ToList();

            foreach (var game in sorted)
            {
                Games.Add(game);
                FilteredGames.Add(game);
            }

            TotalGames = Games.Count;
            InstalledCount = Games.Count(g => g.IsInstalled);

            Debug.WriteLine($"[Library] Zobrazené: {TotalGames} hier, nainštalované: {InstalledCount}");
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