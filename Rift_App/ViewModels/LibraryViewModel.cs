using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json.Linq;
using Rift_App.Models;
using Rift_App.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

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

                // Zdroj 1 — Full Community XML
                var fullLibrary = await ApiService.GetFullLibraryAsync(steamId);
                Debug.WriteLine($"[Library] Community XML: {fullLibrary.Count}");

                // Zdroj 2 — API fallback
                var apiGames = fullLibrary.Count > 0
                    ? fullLibrary
                    : await ApiService.GetLibraryAsync(steamId);

                // Zdroj 3 — Nainštalované .acf
                var installedGames = SteamInstallService.GetAllGames();
                Debug.WriteLine($"[Library] Nainštalované: {installedGames.Count}");

                // Zdroj 4 — localconfig + sharedconfig
                var localConfigGames = SteamInstallService.GetAllAppsFromLocalConfig();
                Debug.WriteLine($"[Library] LocalConfig: {localConfigGames.Count}");

                // Zdroj 5 — Windows Registry (F2P, CoD, BF, Delta Force...)
                var registryGames = SteamInstallService.GetAllAppsFromRegistry();
                Debug.WriteLine($"[Library] Registry: {registryGames.Count}");

                // Spoj všetko
                var allById = new Dictionary<int, GameModel>();
                foreach (var g in registryGames) allById[g.AppId] = g;
                foreach (var g in localConfigGames) allById[g.AppId] = g;
                foreach (var g in installedGames) allById[g.AppId] = g;
                foreach (var g in apiGames) allById[g.AppId] = g; 

                foreach (var g in localConfigGames) allById[g.AppId] = g;
                foreach (var g in installedGames) allById[g.AppId] = g;
                foreach (var g in apiGames) allById[g.AppId] = g;

                var allGames = allById.Values.Where(g => g.AppId > 0).ToList();
                Debug.WriteLine($"[Library] Celkovo unikátnych: {allGames.Count}");

                // Playtime
                var playtime = SteamworksService.GetPlaytimeMinutes();
                foreach (var game in allGames)
                {
                    if (playtime.TryGetValue(game.AppId, out int minutes))
                        game.PlaytimeMinutes = minutes;
                }

                // Doplň názvy pre hry bez názvu
                var missingNames = allGames
                    .Where(g => g.Name == g.AppId.ToString())
                    .ToList();

                if (missingNames.Any())
                {
                    Debug.WriteLine($"[Library] Doplňujem názvy pre {missingNames.Count} hier...");
                    await FillMissingNamesAsync(missingNames);
                }

                // Vyhoď len tie čo stále nemajú názov
                allGames = allGames
            .Where(g => !SteamInternalIds.Contains(g.AppId))
            .Where(g => !string.IsNullOrEmpty(g.Name))
            .Where(g => !g.Name.StartsWith("__REMOVE_"))
            .Where(g => g.Name != g.AppId.ToString())
            .ToList();

                Debug.WriteLine($"[Library] Po filtrácii: {allGames.Count}");

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

        private static readonly HashSet<int> SteamInternalIds = new()
{
    7, 460, 480, 760, 764, 765, 766, 767, 1007, 1406,
    1520, 228980, 241100, 242550, 1430110
};
        // Doplní názvy pre hry z localconfig cez Steam store API
        private async Task FillMissingNamesAsync(List<GameModel> games)
        {
            const int batchSize = 5;
            for (int i = 0; i < games.Count; i += batchSize)
            {
                var chunk = games.Skip(i).Take(batchSize).ToList();
                var tasks = chunk.Select(async game =>
                {
                    try
                    {
                        var details = await ApiService.GetGameDetailsAsync(game.AppId);
                        if (details != null && !string.IsNullOrEmpty(details.Name))
                        {
                            game.Name = details.Name;
                            Debug.WriteLine($"[Library] Názov doplnený: {game.Name} ({game.AppId})");
                        }
                        else
                        {
                            // Žiadna store page = tool/internal app — označ na zmazanie
                            game.Name = $"__REMOVE_{game.AppId}";
                        }
                    }
                    catch
                    {
                        game.Name = $"__REMOVE_{game.AppId}";
                    }
                });
                await Task.WhenAll(tasks);
                await Task.Delay(400);
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
                  //  .OrderByDescending(g => g.IsInstalled)          // nainštalované prvé
                  //   .ThenByDescending(g => g.PlaytimeMinutes)        // potom podľa playtime
                  // .ThenBy(g => g.Name)                             // abecedne zvyšok
                  .OrderByDescending(g => g.Name)
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