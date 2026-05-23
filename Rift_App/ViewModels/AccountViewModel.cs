using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Rift_App.Models;
using Rift_App.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Rift_App.ViewModels
{
    public partial class AccountViewModel : ObservableObject
    {
        // ─── CACHE TTL ────────────────────────────────────────────────────
        private static readonly TimeSpan CacheTTL = TimeSpan.FromHours(1);

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        private static string CachePath =>
            Path.Combine(AppPaths.Ensure(AppPaths.Account(SessionManager.SteamId64)), "snapshot.json");

        private static string HeadersFolder =>
            AppPaths.Ensure(AppPaths.AccountHeaders(SessionManager.SteamId64));

        // ─── LOCK ─────────────────────────────────────────────────────────
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        // ─── SNAPSHOT ─────────────────────────────────────────────────────
        private class AccountSnapshot
        {
            public DateTime SavedAt { get; set; }
            public string AvatarUrl { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public string OnlineStatus { get; set; } = "Online";
            public int Level { get; set; }
            public int GamesOwnedCount { get; set; }
            public int WishlistCount { get; set; }
            public int FriendsCount { get; set; }
            public bool FriendsPrivate { get; set; }
            public double TotalHours2W { get; set; }
            public List<FriendModel> Friends { get; set; } = new();
            public List<RecentActivityGame> RecentGames { get; set; } = new();
        }

        // ─── PROPERTIES ───────────────────────────────────────────────────
        [ObservableProperty] private string _avatarUrl = SessionManager.AvatarUrl;
        [ObservableProperty] private string _username = SessionManager.Username;
        [ObservableProperty] private string _onlineStatus = "Online";
        [ObservableProperty] private int _level = 0;
        [ObservableProperty] private int _gamesOwnedCount = 0;
        [ObservableProperty] private int _wishlistCount = 0;
        [ObservableProperty] private int _friendsCount = 0;
        [ObservableProperty] private bool _friendsPrivate = false;
        [ObservableProperty] private double _totalHours2W = 0;
        [ObservableProperty] private bool _isLoading = true;

        public string StatusColor => OnlineStatus switch
        {
            "Online" or "In-Game" => "#57CBB7",
            _ => "#888888"
        };

        partial void OnOnlineStatusChanged(string value) =>
            OnPropertyChanged(nameof(StatusColor));

        public string TotalHours2WDisplay =>
      TotalHours2W == 1
          ? "1 hour past 2 weeks"
          : $"{TotalHours2W} hours past 2 weeks";

        partial void OnTotalHours2WChanged(double value) =>
            OnPropertyChanged(nameof(TotalHours2WDisplay));

        public ObservableCollection<FriendModel> Friends { get; } = new();
        public ObservableCollection<RecentActivityGame> RecentGames { get; } = new();

        // ─── LOAD ─────────────────────────────────────────────────────────
        [RelayCommand]
        public async Task LoadAsync()
        {
            Username = SessionManager.Username;
            AvatarUrl = SessionManager.AvatarUrl;

            var cached = await TryLoadCacheAsync();
            if (cached != null)
            {
                ApplySnapshot(cached);
                IsLoading = false;
                _ = RefreshInBackgroundAsync();
                return;
            }

            IsLoading = true;
            var snap = await BuildSnapshotAsync();
            ApplySnapshot(snap);
            await SaveCacheAsync(snap);
            IsLoading = false;
        }

        // ─── NAVIGATION ───────────────────────────────────────────────────
        [RelayCommand] private void GoToLibrary() => ViewNavigator.Instance?.MainViewModel?.ShowLibrary();
        [RelayCommand] private void GoToWishlist() => ViewNavigator.Instance?.MainViewModel?.ShowWishlist();
        [RelayCommand] private void GoToStore() => ViewNavigator.Instance?.MainViewModel?.ShowStore();
        [RelayCommand] private void SwitchAccount() => ViewNavigator.Instance?.MainViewModel?.SwitchAccount();

        // ─── COMMAND ───────────────────────────────────────────────────
        [RelayCommand]
        private void ShowSteamFriends()
        {
            OpenSteamUrl("steam://open/friends");
        }

        [RelayCommand]
        private void OpenAchievements(RecentActivityGame game)
        {
            if (game == null) return;
            try
            {
                OpenSteamUrl($"steam://openurl/https://steamcommunity.com/stats/{game.AppId}/achievements");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountVM] OpenGameAchievements error: {ex.Message}");
            }
        }

        // ─── BACKGROUND REFRESH ───────────────────────────────────────────
        private async Task RefreshInBackgroundAsync()
        {
            if (!await _loadLock.WaitAsync(0)) return;
            try
            {
                await Task.Delay(3000);
                var snap = await BuildSnapshotAsync();
                ApplySnapshot(snap);
                await SaveCacheAsync(snap);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountVM] Refresh error: {ex.Message}");
            }
            finally
            {
                _loadLock.Release();
            }
        }

        // ─── BUILD SNAPSHOT ───────────────────────────────────────────────
        private async Task<AccountSnapshot> BuildSnapshotAsync()
        {
            var steamId = SessionManager.SteamId64;

            var snap = new AccountSnapshot
            {
                SavedAt = DateTime.UtcNow,
                Username = SessionManager.Username,
                AvatarUrl = SessionManager.AvatarUrl,
            };

            var levelTask = ApiService.GetSteamLevelAsync(steamId);
            var friendsTask = ApiService.GetFriendsAsync(steamId);
            var activityTask = ApiService.GetRecentActivityAsync(steamId);
            var wishlistCountTask = WishlistCountCache.GetAsync();
            var playerTask = ApiService.GetPlayerInfoAsync(steamId);

            await Task.WhenAll(levelTask, friendsTask, activityTask, wishlistCountTask, playerTask);

            snap.Level = levelTask.Result;
            snap.GamesOwnedCount = await GetBestGameCountAsync(steamId);
            snap.WishlistCount = wishlistCountTask.Result;
            WishlistCountCache.Set(snap.WishlistCount);

            var fr = friendsTask.Result;
            snap.FriendsPrivate = fr?.IsPrivate ?? false;
            snap.FriendsCount = fr?.Friends?.Count ?? 0;
            snap.Friends = fr?.Friends ?? new();

            var player = playerTask.Result;
            snap.OnlineStatus = player?.OnlineStatus ?? "Offline";
            if (player != null)
            {
                snap.AvatarUrl = player.AvatarUrl;
                SessionManager.SetAvatar(player.AvatarUrl);
            }

            var activity = activityTask.Result;
            snap.TotalHours2W = activity?.TotalHours ?? 0;
            snap.RecentGames = await EnrichRecentGamesAsync(
                                    activity?.Games ?? new(), steamId);

            return snap;
        }

        // ─── GAME COUNT — best available source ──────────────────────────
        private static async Task<int> GetBestGameCountAsync(string steamId)
        {
            // 1. Local library cache (most complete — includes F2P, installed, registry)
            var libCached = await LibraryCacheService.LoadAsync(steamId);
            if (libCached != null && libCached.Count > 0)
                return libCached.Count;

            // 2. Steamworks installed games count as lower bound
            int installedCount = 0;
            try
            {
                var installed = SteamInstallService.GetAllGames();
                installedCount = installed.Count;
            }
            catch { }

            // 3. Steam API
            try
            {
                var apiLib = await ApiService.GetLibraryAsync(steamId);
                return Math.Max(apiLib?.Count ?? 0, installedCount);
            }
            catch
            {
                return installedCount;
            }
        }

        // ─── ENRICH RECENT GAMES ─────────────────────────────────────────
        private static async Task<List<RecentActivityGame>> EnrichRecentGamesAsync(List<RecentActivityGame> games, string steamId)
        {
            var headersFolder = AppPaths.Ensure(AppPaths.AccountHeaders(steamId));

            // Ensure LastPlayed cache is ready
            await LastPlayedCacheService.InitializeAsync();

            foreach (var game in games)
            {
                try
                {
                    // ── 1. Header image ───────────────────────────────────
                    var localPath = Path.Combine(headersFolder, $"{game.AppId}_header.jpg");

                    if (!File.Exists(localPath))
                    {
                        // Reuse shared hero if it was downloaded for Library
                        var sharedHero = GameDetailCacheService.GetHeroPath(game.AppId);
                        if (File.Exists(sharedHero))
                            localPath = sharedHero;
                        else
                        {
                            bool ok = await TryDownloadImageAsync(
                                $"https://cdn.akamai.steamstatic.com/steam/apps/{game.AppId}/library_hero.jpg",
                                localPath);
                            if (!ok)
                                await TryDownloadImageAsync(
                                    $"https://cdn.akamai.steamstatic.com/steam/apps/{game.AppId}/header.jpg",
                                    localPath);
                        }
                    }

                    if (File.Exists(localPath))
                        game.HeaderImageUrl = localPath;

                    // ── 2. Last played ────────────────────────────────────
                    var lastPlayed = LastPlayedCacheService.Get(game.AppId);
                    if (lastPlayed.HasValue)
                    {
                        var diff = (DateTime.UtcNow.Date - lastPlayed.Value.Date).Days;
                        game.LastPlayedDisplay = diff switch
                        {
                            0 => "last played today",
                            1 => "last played yesterday",
                            _ => $"last played on {lastPlayed.Value.ToString("d MMM", CultureInfo.InvariantCulture)}"
                        };
                    }

                    // ── 3. Achievements ───────────────────────────────────
                    // Try local cache first
                    var achs = await LibraryCacheService.LoadAchievementsAsync(steamId, game.AppId);

                    if (!achs.HasValue && SteamworksService.IsInitialized)
                    {
                        // Not in library cache — fetch independently and save so
                        // Library can reuse it later without re-downloading
                        Debug.WriteLine($"[AccountVM] Fetching achievements for {game.AppId} independently.");
                        var detail = await SteamworksService.GetAchievementsForAppAsync(game.AppId);
                        if (detail != null)
                        {
                            var unlockedList = detail.Achievements.Where(a => a.Unlocked).ToList();
                            var lockedList = detail.Achievements.Where(a => !a.Unlocked).ToList();
                            await LibraryCacheService.SaveAchievementsAsync(
                                steamId, game.AppId, lockedList, unlockedList);
                            // Re-read from cache so we use the same path
                            achs = await LibraryCacheService.LoadAchievementsAsync(steamId, game.AppId);
                        }
                    }

                    if (achs.HasValue)
                    {
                        var unlocked = achs.Value.Unlocked;
                        var total = unlocked.Count + achs.Value.Locked.Count;

                        game.AchievementsUnlocked = unlocked.Count;
                        game.AchievementsTotal = total;

                        // Recent achievement icons — newest first, max 5 + overflow badge
                        var recent = unlocked
                            .Where(a => a.UnlockTime.HasValue)
                            .OrderByDescending(a => a.UnlockTime)
                            .ToList();

                        game.RecentIcons = recent.Take(5).Select(a => new RecentAchIcon
                        {
                            IconUrl = !string.IsNullOrEmpty(a.LocalIconPath)
                                         ? a.LocalIconPath : a.IconUrl,
                            Name = a.Name
                        }).ToList();

                        game.ExtraIconCount = Math.Max(0, recent.Count - 5);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AccountVM] Enrich {game.AppId}: {ex.Message}");
                }
            }

            return games;
        }

        // ─── APPLY SNAPSHOT ───────────────────────────────────────────────
        private void ApplySnapshot(AccountSnapshot snap)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Username = snap.Username;
                AvatarUrl = snap.AvatarUrl;
                OnlineStatus = snap.OnlineStatus;
                Level = snap.Level;
                GamesOwnedCount = snap.GamesOwnedCount;
                WishlistCount = snap.WishlistCount;
                FriendsCount = snap.FriendsCount;
                FriendsPrivate = snap.FriendsPrivate;
                TotalHours2W = snap.TotalHours2W;

                // Only rebuild lists when content actually changed
                var newFriendIds = snap.Friends.Select(f => f.SteamId).ToList();
                var currFriendIds = Friends.Select(f => f.SteamId).ToList();
                if (!newFriendIds.SequenceEqual(currFriendIds))
                {
                    Friends.Clear();
                    foreach (var f in snap.Friends) Friends.Add(f);
                }

                var newGameIds = snap.RecentGames.Select(g => g.AppId).ToList();
                var currGameIds = RecentGames.Select(g => g.AppId).ToList();
                if (!newGameIds.SequenceEqual(currGameIds))
                {
                    RecentGames.Clear();
                    foreach (var g in snap.RecentGames) RecentGames.Add(g);
                }
            });
        }

        // ─── IMAGE HELPERS ────────────────────────────────────────────────
        private static async Task<bool> TryDownloadImageAsync(string url, string savePath)
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(savePath, bytes);
                return true;
            }
            catch { return false; }
        }

        // ─── CACHE I/O ────────────────────────────────────────────────────
        private async Task<AccountSnapshot?> TryLoadCacheAsync()
        {
            try
            {
                var path = CachePath;
                if (!File.Exists(path)) return null;

                var json = await File.ReadAllTextAsync(path);
                var snap = JsonConvert.DeserializeObject<AccountSnapshot>(json);
                if (snap == null || DateTime.UtcNow - snap.SavedAt > CacheTTL) return null;

                // Restore local image paths
                var headersFolder = HeadersFolder;
                foreach (var g in snap.RecentGames)
                {
                    var local = Path.Combine(headersFolder, $"{g.AppId}_header.jpg");
                    if (File.Exists(local))
                        g.HeaderImageUrl = local;
                    else
                    {
                        var shared = GameDetailCacheService.GetHeroPath(g.AppId);
                        if (File.Exists(shared)) g.HeaderImageUrl = shared;
                    }
                }

                return snap;
            }
            catch { return null; }
        }

        private async Task SaveCacheAsync(AccountSnapshot snap)
        {
            try
            {
                await File.WriteAllTextAsync(CachePath,
                    JsonConvert.SerializeObject(snap, Formatting.None));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountVM] Cache save: {ex.Message}");
            }
        }

        // ─── HELPER ───────────────────────────────────────────────────────
        private static void OpenSteamUrl(string url)
        {
            if (!SteamworksService.IsSteamInstalled())
            {
                MessageBox.Show(
                    "Steam is not installed on this computer.",
                    "Steam Not Found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
    }
}