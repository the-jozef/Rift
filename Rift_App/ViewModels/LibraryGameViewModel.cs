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
using System.Net.Http;
using System.Text;
using System.Windows.Media.Imaging;

namespace Rift_App.ViewModels
{
    public partial class LibraryGameViewModel : ObservableObject
    {
        // ─── PROPERTIES ───────────────────────────────────────────────────

        [ObservableProperty] private GameModel? _game;
        [ObservableProperty] private GameDetailModel? _detail;
        [ObservableProperty] private BitmapImage? _heroImage;
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private bool _isInstalled = false;
        [ObservableProperty] private bool _needsUpdate = false;
        [ObservableProperty] private bool _hasGame = false;

        public string ButtonText =>
            NeedsUpdate ? "UPDATE" :
            IsInstalled ? "PLAY" : "INSTALL";

        public bool HasUnlockedRemaining => UnlockedRemaining > 0;
        public bool HasLockedRemaining => LockedRemaining > 0;
        public int UnlockedRemaining { get; private set; }
        public int LockedRemaining { get; private set; }

        public ObservableCollection<AchievementModel> UnlockedPreview { get; } = new();
        public ObservableCollection<AchievementModel> LockedPreview { get; } = new();
        public ObservableCollection<AchievementDateGroup> RecentActivity { get; } = new();

        public string CurrentUsername => SessionManager.Username;
        public string CurrentAvatarUrl => SessionManager.AvatarUrl;

        // ─── LOAD ─────────────────────────────────────────────────────────

        public async Task LoadAsync(GameModel game)
        {
            HasGame = true;
            IsLoading = true;
            Game = game;
            HeroImage = null;
            Detail = null;
            UnlockedPreview.Clear();
            LockedPreview.Clear();
            RecentActivity.Clear();

            try
            {
                // 1. Install status — reads local .acf files
                var info = SteamInstallService.GetInfo(game.AppId);
                IsInstalled = info.IsInstalled;
                NeedsUpdate = info.NeedsUpdate;
                OnPropertyChanged(nameof(ButtonText));

                // 2. Hero image — local disk first
                var heroPath = await GameDetailCacheService.EnsureHeroImageAsync(game.AppId);
                HeroImage = LoadBitmapFromPath(heroPath);

                // 3. Detail — 24h cache first
                var cached = await GameDetailCacheService.LoadAsync(game.AppId);
                if (cached != null)
                {
                    RestoreLocalIconPaths(cached);
                    Detail = cached;
                    BuildPreviews();
                    IsLoading = false;

                    // Background refresh if cache older than 20h
                    if (DateTime.UtcNow - cached.CachedAt > TimeSpan.FromHours(20))
                        _ = RefreshInBackgroundAsync(game.AppId);

                    return;
                }

                // 4. No cache — load from Steamworks
                var fresh = await LoadFromSteamworksAsync(game.AppId);
                if (fresh != null)
                {
                    fresh.AppId = game.AppId;
                    RestoreLocalIconPaths(fresh);
                    await GameDetailCacheService.SaveAsync(fresh);
                    Detail = fresh;
                    BuildPreviews();
                }
                else
                {
                    Debug.WriteLine($"[LibraryGame] No data for {game.AppId}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibraryGame] Load error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ─── COMMANDS ─────────────────────────────────────────────────────

        [RelayCommand]
        private void LaunchOrInstall()
        {
            if (Game == null) return;
            var uri = IsInstalled
                ? $"steam://run/{Game.AppId}"
                : $"steam://install/{Game.AppId}";
            OpenUri(uri);
        }

        [RelayCommand]
        private void OpenStore()
        {
            if (Game != null) OpenUri($"steam://store/{Game.AppId}");
        }

        // ─── STEAMWORKS LOAD ──────────────────────────────────────────────

        private static async Task<GameDetailModel?> LoadFromSteamworksAsync(int appId)
        {
            if (!SteamworksService.IsInitialized)
            {
                Debug.WriteLine($"[LibraryGame] Steamworks not initialized for {appId}");
                return null;
            }

            var detail = await SteamworksService.GetAchievementsForAppAsync(appId);
            Debug.WriteLine($"[LibraryGame] Steamworks loaded {detail?.Achievements?.Count} achievements for {appId}");
            return detail;
        }

        private async Task RefreshInBackgroundAsync(int appId)
        {
            try
            {
                var fresh = await LoadFromSteamworksAsync(appId);
                if (fresh == null) return;

                fresh.AppId = appId;
                RestoreLocalIconPaths(fresh);
                await GameDetailCacheService.SaveAsync(fresh);

                if (Game?.AppId == appId)
                {
                    Detail = fresh;
                    BuildPreviews();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibraryGame] Background refresh error: {ex.Message}");
            }
        }

        // ─── BUILD PREVIEWS ───────────────────────────────────────────────

        private void BuildPreviews()
        {
            UnlockedPreview.Clear();
            LockedPreview.Clear();
            RecentActivity.Clear();

            if (Detail == null) return;

            var allUnlocked = Detail.Achievements.Where(a => a.Unlocked).ToList();
            var locked = Detail.Achievements.Where(a => !a.Unlocked).ToList();
          
            var mostRecent = allUnlocked
                .Where(a => a.UnlockTime.HasValue)
                .OrderByDescending(a => a.UnlockTime)
                .FirstOrDefault();

            var rarestUnlocked = allUnlocked
                .Where(a => a != mostRecent)
                .OrderBy(a => a.RarityPercentage)
                .Take(5)
                .ToList();

            foreach (var a in rarestUnlocked)
                UnlockedPreview.Add(a);

            var rarestLocked = locked
                .OrderBy(a => a.RarityPercentage)
                .Take(5)
                .ToList();

            foreach (var a in rarestLocked)
                LockedPreview.Add(a);

            UnlockedRemaining = Math.Max(0,
                allUnlocked.Count
                - rarestUnlocked.Count
                - (mostRecent != null ? 1 : 0));

            LockedRemaining = Math.Max(0, locked.Count - rarestLocked.Count);

            OnPropertyChanged(nameof(UnlockedRemaining));
            OnPropertyChanged(nameof(LockedRemaining));

            // Recent Activity 
            var groups = allUnlocked
                .Where(a => a.UnlockTime.HasValue)
                .OrderByDescending(a => a.UnlockTime)
                .GroupBy(a => a.UnlockTime!.Value.Date)
                .Take(3)
                .Select(g => new AchievementDateGroup
                {
                    DateLabel = g.Key.ToString("MMMM d", CultureInfo.InvariantCulture),
                    Items = g.ToList()
                });

            foreach (var group in groups)
                RecentActivity.Add(group);
        }

        // ─── LOCAL ICON PATHS ─────────────────────────────────────────────
        // Sets LocalIconPath + LocalIconGrayPath on each achievement     

        private static void RestoreLocalIconPaths(GameDetailModel detail)
        {
            foreach (var a in detail.Achievements)
            {
                a.LocalIconPath = SteamworksService.GetLocalIconPath(detail.AppId, a.ApiName, gray: false);
                a.LocalIconGrayPath = SteamworksService.GetLocalIconPath(detail.AppId, a.ApiName, gray: true);
                a.ResetIconImage(); // Force reload with new paths
            }
        }

        // ─── HELPERS ──────────────────────────────────────────────────────

        private static BitmapImage? LoadBitmapFromPath(string? path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        private static void OpenUri(string uri)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = uri,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}