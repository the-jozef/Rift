using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Rift_App.Models;
using Rift_App.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Rift_App.ViewModels
{
    public partial class AccountViewModel : ObservableObject
    {
        // ─── CACHE PATH ───────────────────────────────────────────────────
        private static readonly string CacheFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RiftApp", "cache");

        private static string AccountCachePath =>
            Path.Combine(CacheFolder, $"account_{SessionManager.SteamId64}.json");

        private static readonly TimeSpan CacheTTL = TimeSpan.FromHours(1);

        // ─── PROPERTIES ───────────────────────────────────────────────────

        [ObservableProperty] private string _username = SessionManager.Username;
        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private string _avatarUrl = SessionManager.AvatarUrl;
        [ObservableProperty] private int _level = 0;
        [ObservableProperty] private int _gamesOwnedCount = 0;
        [ObservableProperty] private int _wishlistCount = 0;
        [ObservableProperty] private int _friendsCount = 0;
        [ObservableProperty] private bool _isLoading = true;
        [ObservableProperty] private bool _friendsPrivate = false;

        // Recent achievements across all games (max 5)
        public ObservableCollection<RecentAchievementEntry> RecentAchievements { get; } = new();

        // Friends list
        public ObservableCollection<FriendModel> Friends { get; } = new();

        // ─── LOAD ─────────────────────────────────────────────────────────

        [RelayCommand]
        public async Task LoadAsync()
        {
            IsLoading = true;
            Username = SessionManager.Username;
            AvatarUrl = SessionManager.AvatarUrl;

            // Try disk cache first
            var cached = await TryLoadCacheAsync();
            if (cached != null)
            {
                ApplyCache(cached);
                IsLoading = false;
                _ = RefreshInBackgroundAsync();
                return;
            }

            await FetchAllAsync();
            IsLoading = false;
        }

        // ─── NAVIGATION COMMANDS ──────────────────────────────────────────

        [RelayCommand]
        private void GoToLibrary() =>
            ViewNavigator.Instance?.MainViewModel?.ShowLibrary();

        [RelayCommand]
        private void GoToWishlist() =>
            ViewNavigator.Instance?.MainViewModel?.ShowWishlist();

        // ─── FETCH ────────────────────────────────────────────────────────

        private async Task FetchAllAsync()
        {
            var steamId = SessionManager.SteamId64;

            // Run independent fetches in parallel
            var levelTask = ApiService.GetSteamLevelAsync(steamId);
            var friendsTask = ApiService.GetFriendsAsync(steamId);
            var libraryTask = ApiService.GetLibraryAsync(steamId);
            var wishlistTask = ApiService.GetWishlistIdsAsync(steamId);

            await Task.WhenAll(levelTask, friendsTask, libraryTask, wishlistTask);

            // Level
            Level = levelTask.Result;

            // Library count
            GamesOwnedCount = libraryTask.Result?.Count ?? 0;

            // Wishlist count
            WishlistCount = wishlistTask.Result?.Count ?? 0;

            // Friends
            var friendsResp = friendsTask.Result;
            FriendsPrivate = friendsResp?.IsPrivate ?? false;
            FriendsCount = friendsResp?.Friends?.Count ?? 0;

            Application.Current.Dispatcher.Invoke(() =>
            {
                Friends.Clear();
                if (friendsResp?.Friends != null)
                    foreach (var f in friendsResp.Friends)
                        Friends.Add(f);
            });

            // Recent achievements across all library games
            await LoadRecentAchievementsAsync(libraryTask.Result ?? new());

            // Save to cache
            await SaveCacheAsync();
        }

        private async Task LoadRecentAchievementsAsync(List<GameModel> library)
        {
            var steamId = SessionManager.SteamId64;
            var allRecent = new List<RecentAchievementEntry>();

            // Check cached achievement files — no extra API calls needed
            foreach (var game in library)
            {
                try
                {
                    var achs = await LibraryCacheService.LoadAchievementsAsync(steamId, game.AppId);
                    if (!achs.HasValue) continue;

                    var unlocked = achs.Value.Unlocked
                        .Where(a => a.UnlockTime.HasValue)
                        .OrderByDescending(a => a.UnlockTime)
                        .Take(1) // newest per game
                        .ToList();

                    foreach (var a in unlocked)
                    {
                        allRecent.Add(new RecentAchievementEntry
                        {
                            GameName = game.Name,
                            GameAppId = game.AppId,
                            AchievementName = a.Name,
                            Description = a.Description,
                            IconUrl = !string.IsNullOrEmpty(a.LocalIconPath)
                                             ? a.LocalIconPath : a.IconUrl,
                            UnlockTime = a.UnlockTime!.Value
                        });
                    }
                }
                catch { }
            }

            // Global sort — newest first, keep top 5
            var top5 = allRecent
                .OrderByDescending(e => e.UnlockTime)
                .Take(5)
                .ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                RecentAchievements.Clear();
                foreach (var e in top5)
                    RecentAchievements.Add(e);
            });
        }

        // ─── BACKGROUND REFRESH ───────────────────────────────────────────

        private async Task RefreshInBackgroundAsync()
        {
            try
            {
                await Task.Delay(2000);
                await FetchAllAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountVM] Background refresh error: {ex.Message}");
            }
        }

        // ─── CACHE ────────────────────────────────────────────────────────

        private class AccountCache
        {
            public DateTime SavedAt { get; set; }
            public int Level { get; set; }
            public int GamesOwnedCount { get; set; }
            public int WishlistCount { get; set; }
            public int FriendsCount { get; set; }
            public bool FriendsPrivate { get; set; }
            public List<FriendModel> Friends { get; set; } = new();
            public List<RecentAchievementEntry> RecentAchievements { get; set; } = new();
        }

        private async Task<AccountCache?> TryLoadCacheAsync()
        {
            try
            {
                if (!File.Exists(AccountCachePath)) return null;
                var json = await File.ReadAllTextAsync(AccountCachePath);
                var cache = JsonConvert.DeserializeObject<AccountCache>(json);
                if (cache == null) return null;
                if (DateTime.UtcNow - cache.SavedAt > CacheTTL) return null;
                return cache;
            }
            catch { return null; }
        }

        private void ApplyCache(AccountCache c)
        {
            Level = c.Level;
            GamesOwnedCount = c.GamesOwnedCount;
            WishlistCount = c.WishlistCount;
            FriendsCount = c.FriendsCount;
            FriendsPrivate = c.FriendsPrivate;

            Friends.Clear();
            foreach (var f in c.Friends) Friends.Add(f);

            RecentAchievements.Clear();
            foreach (var a in c.RecentAchievements) RecentAchievements.Add(a);
        }

        private async Task SaveCacheAsync()
        {
            try
            {
                if (!Directory.Exists(CacheFolder))
                    Directory.CreateDirectory(CacheFolder);

                var cache = new AccountCache
                {
                    SavedAt = DateTime.UtcNow,
                    Level = Level,
                    GamesOwnedCount = GamesOwnedCount,
                    WishlistCount = WishlistCount,
                    FriendsCount = FriendsCount,
                    FriendsPrivate = FriendsPrivate,
                    Friends = Friends.ToList(),
                    RecentAchievements = RecentAchievements.ToList()
                };

                var json = JsonConvert.SerializeObject(cache, Formatting.None);
                await File.WriteAllTextAsync(AccountCachePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountVM] Cache save error: {ex.Message}");
            }
        }
    }

    // ─── HELPER MODEL ─────────────────────────────────────────────────────

    public class RecentAchievementEntry
    {
        public string GameName { get; set; } = string.Empty;
        public int GameAppId { get; set; }
        public string AchievementName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
        public DateTime UnlockTime { get; set; }

        public string UnlockTimeDisplay
        {
            get
            {
                var diff = DateTime.UtcNow - UnlockTime;
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
                return UnlockTime.ToString("MMM d");
            }
        }
    }
}