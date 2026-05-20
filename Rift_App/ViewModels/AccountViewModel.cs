using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Rift_App.Models;
using Rift_App.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Threading;
using System.Net.Http;

namespace Rift_App.ViewModels
{
    public partial class AccountViewModel : ObservableObject
    {
        // ─── CACHE ────────────────────────────────────────────────────────
        private static readonly string CacheFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RiftApp", "cache");

        private static readonly string ImagesFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RiftApp", "account_headers");

        private static string CachePath =>
            Path.Combine(CacheFolder, $"account_{SessionManager.SteamId64}.json");

        private static readonly TimeSpan CacheTTL = TimeSpan.FromHours(1);

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

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
            TotalHours2W > 0
                ? $"{TotalHours2W} hours past 2 weeks"
                : "0 hours past 2 weeks";

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
            var libraryTask = ApiService.GetLibraryAsync(steamId);
            var activityTask = ApiService.GetRecentActivityAsync(steamId);
            var wishlistCountTask = WishlistCountCache.GetAsync();

            await Task.WhenAll(levelTask, friendsTask, libraryTask,
                               activityTask, wishlistCountTask);

            snap.Level = levelTask.Result;
            snap.GamesOwnedCount = libraryTask.Result?.Count ?? 0;
            snap.WishlistCount = wishlistCountTask.Result;
            WishlistCountCache.Set(snap.WishlistCount);

            var fr = friendsTask.Result;
            snap.FriendsPrivate = fr?.IsPrivate ?? false;
            snap.FriendsCount = fr?.Friends?.Count ?? 0;
            snap.Friends = fr?.Friends ?? new();
            snap.OnlineStatus = "Online";

            var activity = activityTask.Result;
            snap.TotalHours2W = activity?.TotalHours ?? 0;
            snap.RecentGames = await EnrichRecentGamesAsync(
                                    activity?.Games ?? new(), steamId);

            return snap;
        }

        // ─── ENRICH RECENT GAMES ─────────────────────────────────────────
        private static async Task<List<RecentActivityGame>> EnrichRecentGamesAsync(
            List<RecentActivityGame> games, string steamId)
        {
            EnsureImageFolder();

            foreach (var game in games)
            {
                try
                {
                    // ── 1. Ensure header image on disk ────────────────────
                    var localPath = GetHeaderPath(game.AppId);
                    if (!File.Exists(localPath))
                    {
                        // Try library_hero first, fallback to header.jpg
                        var downloaded = await TryDownloadImageAsync(
                            $"https://cdn.akamai.steamstatic.com/steam/apps/{game.AppId}/library_hero.jpg",
                            localPath);

                        if (!downloaded)
                            await TryDownloadImageAsync(
                                $"https://cdn.akamai.steamstatic.com/steam/apps/{game.AppId}/header.jpg",
                                localPath);
                    }

                    // Use local path if exists, otherwise keep URL
                    if (File.Exists(localPath))
                        game.HeaderImageUrl = localPath;

                    // ── 2. Achievement data from local cache ──────────────
                    var achs = await LibraryCacheService.LoadAchievementsAsync(
                                   steamId, game.AppId);
                    if (!achs.HasValue) continue;

                    var unlocked = achs.Value.Unlocked;
                    var total = unlocked.Count + achs.Value.Locked.Count;

                    game.AchievementsUnlocked = unlocked.Count;
                    game.AchievementsTotal = total;

                    // Last played
                    var lastPlayed = LastPlayedCacheService.Get(game.AppId);
                    if (lastPlayed.HasValue)
                    {
                        var diff = (DateTime.UtcNow.Date - lastPlayed.Value.Date).Days;
                        game.LastPlayedDisplay = diff switch
                        {
                            0 => "last played today",
                            1 => "last played yesterday",
                            _ => $"last played on {lastPlayed.Value:d MMM}"
                        };
                    }

                    // Recent achievement icons — newest first
                    var recent = unlocked
                        .Where(a => a.UnlockTime.HasValue)
                        .OrderByDescending(a => a.UnlockTime)
                        .ToList();

                    var icons = new List<RecentAchIcon>();
                    foreach (var a in recent.Take(5))
                    {
                        icons.Add(new RecentAchIcon
                        {
                            IconUrl = !string.IsNullOrEmpty(a.LocalIconPath)
                                      ? a.LocalIconPath : a.IconUrl,
                            Name = a.Name
                        });
                    }

                    int extra = recent.Count - 5;
                    if (extra > 0)
                        icons.Add(new RecentAchIcon { ExtraCount = extra });

                    game.RecentIcons = icons;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AccountVM] Enrich {game.AppId}: {ex.Message}");
                }
            }

            return games;
        }

        // ─── APPLY SNAPSHOT (ATOMIC) ──────────────────────────────────────
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
        private static string GetHeaderPath(int appId) =>
            Path.Combine(ImagesFolder, $"{appId}_header.jpg");

        private static void EnsureImageFolder()
        {
            if (!Directory.Exists(ImagesFolder))
                Directory.CreateDirectory(ImagesFolder);
        }

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
                if (!File.Exists(CachePath)) return null;
                var json = await File.ReadAllTextAsync(CachePath);
                var snap = JsonConvert.DeserializeObject<AccountSnapshot>(json);
                if (snap == null) return null;
                if (DateTime.UtcNow - snap.SavedAt > CacheTTL) return null;

                // Restore local image paths for cached games
                foreach (var g in snap.RecentGames)
                {
                    var localPath = GetHeaderPath(g.AppId);
                    if (File.Exists(localPath))
                        g.HeaderImageUrl = localPath;
                }

                return snap;
            }
            catch { return null; }
        }

        private async Task SaveCacheAsync(AccountSnapshot snap)
        {
            try
            {
                if (!Directory.Exists(CacheFolder))
                    Directory.CreateDirectory(CacheFolder);
                var json = JsonConvert.SerializeObject(snap, Formatting.None);
                await File.WriteAllTextAsync(CachePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountVM] Cache save: {ex.Message}");
            }
        }
    }
}