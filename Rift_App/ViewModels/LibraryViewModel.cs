using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rift_App.Models;
using Rift_App.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
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

        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private int _totalGames = 0;
        [ObservableProperty] private int _installedCount = 0;

        public event Action<GameModel>? OnGameSelected;

        public LibraryViewModel()
        {
            // Subscribe to Steam changes (playtime, lastplayed)
            SteamCallbackService.LibraryChanged += OnSteamLibraryChanged;
        }


        // ─── BLACKLIST ──────────────────────────────────────────────────
        private static readonly string BlacklistPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "RiftApp", "cache", "appid_blacklist.json");

        private static HashSet<int> LoadBlacklist()
        {
            try
            {
                if (!File.Exists(BlacklistPath)) return new();
                var json = File.ReadAllText(BlacklistPath);
                return JsonConvert.DeserializeObject<HashSet<int>>(json) ?? new();
            }
            catch { return new(); }
        }

        private static void SaveBlacklist(HashSet<int> blacklist)
        {
            try
            {
                var dir = Path.GetDirectoryName(BlacklistPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(BlacklistPath, JsonConvert.SerializeObject(blacklist));
            }
            catch { }
        }

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
            /*//delete
            if (File.Exists(BlacklistPath))
            {
                File.Delete(BlacklistPath);
                Debug.WriteLine("[Library] Old blacklist cleared.");
            }*/
            IsLoading = true;
            Games.Clear();

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

                var fullLibrary = await ApiService.GetFullLibraryAsync(steamId);
                Debug.WriteLine($"[Library] Community XML: {fullLibrary.Count}");

                var apiGames = fullLibrary.Count > 0
                    ? fullLibrary
                    : await ApiService.GetLibraryAsync(steamId);

                var installedGames = SteamInstallService.GetAllGames();
                var localConfigGames = SteamInstallService.GetAllAppsFromLocalConfig();

                // Zdroj 5 — Windows Registry
                var registryGames = SteamInstallService.GetAllAppsFromRegistry();
                Debug.WriteLine($"[Library] Registry: {registryGames.Count}");

                // Zdroj 6 — Subscribed F2P cez Steamworks + SteamSpy
                List<GameModel> subscribedGames = new();
                if (SteamworksService.IsInitialized)
                {
                    var freeGames = await SteamSpyService.GetFreeGamesAsync();
                    subscribedGames = SteamInstallService.GetSubscribedFreeGames(freeGames);
                }
                Debug.WriteLine($"[Library] Subscribed F2P: {subscribedGames.Count}");

                // Spoj všetko — API vyhráva (má reálne mená)
                var allById = new Dictionary<int, GameModel>();
                foreach (var g in registryGames) allById[g.AppId] = g;
                foreach (var g in localConfigGames) allById[g.AppId] = g;
                foreach (var g in subscribedGames) allById[g.AppId] = g;  // NOVÝ RIADOK
                foreach (var g in installedGames) allById[g.AppId] = g;
                foreach (var g in apiGames) allById[g.AppId] = g;

                var allGames = allById.Values
    .Where(g => g.AppId > 0
             && !SteamInternalIds.Contains(g.AppId)
             && !IsDlcOrJunk(g))
    .ToList();

                // Playtime from local VDF
                var playtime = SteamworksService.GetPlaytimeMinutes();
                foreach (var game in allGames)
                    if (playtime.TryGetValue(game.AppId, out int min))
                        game.PlaytimeMinutes = min;

                // ── SPLIT: known names vs. needs API lookup ────────────────
                var knownGames = allGames
                    .Where(g => !string.IsNullOrEmpty(g.Name) && g.Name != g.AppId.ToString())
                    .ToList();

                var unknownGames = allGames
                    .Where(g => string.IsNullOrEmpty(g.Name) || g.Name == g.AppId.ToString())
                    .ToList();

                // Show known games immediately
                CheckInstallStatus(knownGames);
                await LibraryCacheService.DownloadAllIconsAsync(knownGames);
                await LibraryCacheService.SaveAsync(knownGames);
                PopulateGames(knownGames);
                IsLoading = false;

                // Resolve unknown names progressively in background
                if (unknownGames.Any())
                {
                    Debug.WriteLine($"[Library] {unknownGames.Count} games need name lookup (background).");
                    _ = FillNamesProgressiveAsync(unknownGames);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Library] Load error: {ex.Message}");
            }
            finally
            {
                if (IsLoading) IsLoading = false;
                await ApiService.SaveSessionAsync("Library");
            }
        }

        // ─── PROGRESSIVE NAME FILL ────────────────────────────────────────
        // Fetches names 3 at a time, inserts each game alphabetically as resolved.

        private async Task FillNamesProgressiveAsync(List<GameModel> games)
        {
            var steamId = SessionManager.SteamId64;
            var blacklist = LoadBlacklist();
            bool blacklistChanged = false;

            games = games.Where(g => !blacklist.Contains(g.AppId)).ToList();

            Debug.WriteLine($"[Fill] IDs to resolve: {string.Join(", ", games.Select(g => g.AppId))}");

            if (!games.Any()) return;

            const int batchSize = 3;

            for (int i = 0; i < games.Count; i += batchSize)
            {
                var chunk = games.Skip(i).Take(batchSize).ToList();

                var tasks = chunk.Select(async game =>
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                        var details = await ApiService
                            .GetGameDetailsAsync(game.AppId)
                            .WaitAsync(cts.Token);

                        //Debug.WriteLine($"[Fill] {game.AppId} → {(details == null ? "NULL" : details.Name)}");

                        if (details != null && !string.IsNullOrEmpty(details.Name))
                        {
                            game.Name = details.Name;
                            game.IconPath = await LibraryCacheService
                                .DownloadIconAsync(game.AppId, game.IconUrl);
                            return (game, blacklist: false);
                        }

                        // NULL ale bez HTTP exception = hra neexistuje → blacklist
                        return (null, blacklist: true);
                    }
                    catch (HttpRequestException ex)
                    {
                        // Rate limit alebo network error — NESMIE ísť do blacklistu
                        //Debug.WriteLine($"[Fill] {game.AppId} HTTP error (no blacklist): {ex.Message}");
                        return (null, blacklist: false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Fill] {game.AppId} error: {ex.Message}");
                        return (null, blacklist: false);
                    }
                });

                var results = await Task.WhenAll(tasks);

                var resolved = results
                    .Where(r => r.game != null)
                    .Select(r => r.game!)
                    .ToList();

                // Len skutočne neexistujúce hry (nie HTTP errory) idú do blacklistu
                foreach (var ((_, isBlacklisted), originalGame) in results.Zip(chunk))
                {
                    if (isBlacklisted)
                    {
                        lock (blacklist)
                        {
                            blacklist.Add(originalGame.AppId); // originalGame comes straight from chunk
                            blacklistChanged = true;
                        }
                    }
                }

                if (resolved.Any())
                {
                    CheckInstallStatus(resolved);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var game in resolved)
                        {
                            InsertAlphabetically(game);
                            TotalGames = Games.Count;
                            InstalledCount = Games.Count(g => g.IsInstalled);
                        }
                    });

                    foreach (var game in resolved)
                        await LibraryCacheService.SaveGameAsync(steamId, game);
                }

                await Task.Delay(600);
            }

            if (blacklistChanged) SaveBlacklist(blacklist);
            Debug.WriteLine("[Library] Progressive name fill complete.");
        }

        /// Inserts a game into Games keeping alphabetical order.
        private void InsertAlphabetically(GameModel game)
        {
            for (int i = 0; i < Games.Count; i++)
            {
                if (string.Compare(game.Name, Games[i].Name,
                        StringComparison.OrdinalIgnoreCase) <= 0)
                {
                    Games.Insert(i, game);
                    return;
                }
            }
            Games.Add(game);
        }

        // ─── STEAM CHANGE DETECTION ───────────────────────────────────────
        // Called by SteamCallbackService when localconfig.vdf changes
        // (i.e. user played a game and closed it).

        private void OnSteamLibraryChanged()
        {
            Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    var playtime = SteamworksService.GetPlaytimeMinutes();
                    foreach (var game in Games)
                    {
                        if (playtime.TryGetValue(game.AppId, out int minutes))
                            game.PlaytimeMinutes = minutes;
                    }
                    InstalledCount = Games.Count(g => g.IsInstalled);
                    Debug.WriteLine("[Library] Playtime refreshed from Steam change.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Library] OnSteamLibraryChanged error: {ex.Message}");
                }
            });
        }

        // ─── BACKGROUND SYNC ──────────────────────────────────────────────

        private async Task SyncInBackgroundAsync()
        {
            try
            {
                await Task.Delay(3000);

                var steamId = SessionManager.SteamId64;
                var fresh = await ApiService.GetLibraryAsync(steamId);
                if (fresh.Count == 0) return;

                var playtime = SteamworksService.GetPlaytimeMinutes();
                foreach (var game in fresh)
                    if (playtime.TryGetValue(game.AppId, out int minutes))
                        game.PlaytimeMinutes = minutes;

                var cached = Games.ToList();
                var (synced, changed) = await LibraryCacheService.SyncAsync(cached, fresh);
                if (!changed) return;

                RestoreIconPaths(synced);
                CheckInstallStatus(synced);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Games.Clear();
                    PopulateGames(synced);
                });

                await LibraryCacheService.SaveAsync(synced);
                Debug.WriteLine("[Library] Sync done.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Library] Sync error: {ex.Message}");
            }
        }

        // ─── INSTALL STATUS ───────────────────────────────────────────────

        private static void CheckInstallStatus(List<GameModel> games)
        {
            foreach (var game in games)
            {
                var info = SteamworksService.IsInitialized
                    ? SteamworksService.GetInstallInfo(game.AppId)
                    : SteamInstallService.GetInfo(game.AppId);

                game.IsInstalled = info.IsInstalled;
            }
        }

        // ─── HELPERS ──────────────────────────────────────────────────────

        private static readonly HashSet<int> SteamInternalIds = new()
{
    7, 460, 480, 760, 764, 765, 766, 767, 1007, 1406,
    1520, 228980, 241100, 242550, 1430110, 250820, 1059530, 1391110, 1628350,
};

        private static void RestoreIconPaths(List<GameModel> games)
        {
            foreach (var game in games)
            {
                var path = LibraryCacheService.GetIconPath(game.AppId);
                game.IconPath = File.Exists(path) ? path : null;
            }
        }
        private static bool IsDlcOrJunk(GameModel g)
        {
            var t = g.Type?.ToLowerInvariant() ?? "game";
            if (t == "dlc" || t == "demo" || t == "soundtrack" ||
                t == "tool" || t == "beta" || t == "playtest" ||
                t == "prologue") return true;

            if (string.IsNullOrEmpty(g.Name) || g.Name == g.AppId.ToString())
                return false;

            var lower = g.Name.ToLowerInvariant();
            return lower.EndsWith(" - dlc")
                || lower.Contains("soundtrack")
                || lower.Contains("dedicated server")
                || lower.Contains(" - demo")
                || lower.Contains("playtest");
        }

        private void PopulateGames(List<GameModel> games)
        {
            var sorted = games.OrderBy(g => g.Name).ToList();
            foreach (var game in sorted)
                Games.Add(game);

            TotalGames = Games.Count;
            InstalledCount = Games.Count(g => g.IsInstalled);
            Debug.WriteLine($"[Library] Shown: {TotalGames}, installed: {InstalledCount}");
        }
    }
}