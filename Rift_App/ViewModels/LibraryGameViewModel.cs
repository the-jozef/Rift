using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rift_App.Models;
using Rift_App.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows;

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

        public bool HasAchievements => (Detail?.AchievementsTotal ?? 0) > 0;
        public bool HasNoAchievements => (Detail?.AchievementsTotal ?? 0) == 0;
        public bool HasUnlockedRemaining => UnlockedRemaining > 0;
        public bool HasLockedRemaining => LockedRemaining > 0;
        public int UnlockedRemaining { get; private set; }
        public int LockedRemaining { get; private set; }
        public AchievementModel? MostRecentAchievement { get; private set; }
        public string CurrentUsername => SessionManager.Username;
        public string CurrentAvatarUrl => SessionManager.AvatarUrl;

        public ObservableCollection<AchievementModel> UnlockedPreview { get; } = new();
        public ObservableCollection<AchievementModel> LockedPreview { get; } = new();
        public ObservableCollection<AchievementDateGroup> RecentActivity { get; } = new();


        public LibraryGameViewModel()
        {
            SteamCallbackService.LibraryChanged += OnSteamLibraryChanged;
            SteamCallbackService.AchievementUnlocked += OnAchievementUnlocked;
        }


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
                // 1. Install status
                var info = SteamInstallService.GetInfo(game.AppId);
                IsInstalled = info.IsInstalled;
                NeedsUpdate = info.NeedsUpdate;

                // 2. Hero image
                var heroPath = await GameDetailCacheService.EnsureHeroImageAsync(game.AppId);
                HeroImage = LoadBitmap(heroPath);

                // 3. Achievements — cache first
                var steamId = SessionManager.SteamId64;
                var cachedAchs = await LibraryCacheService.LoadAchievementsAsync(steamId, game.AppId);

                if (cachedAchs.HasValue)
                {
                    // Rebuild detail from cache
                    Detail = new GameDetailModel
                    {
                        AppId = game.AppId,
                        Achievements = cachedAchs.Value.Unlocked
                                               .Concat(cachedAchs.Value.Locked).ToList(),
                        AchievementsTotal = cachedAchs.Value.Unlocked.Count +
                                               cachedAchs.Value.Locked.Count,
                        AchievementsUnlocked = cachedAchs.Value.Unlocked.Count
                    };
                    RestoreLocalIconPaths(Detail);
                    BuildPreviews();
                    IsLoading = false;
                    _ = RefreshInBackgroundAsync(game.AppId);
                    return;
                }

                // 4. Fresh load
                var fresh = await LoadFromSteamworksAsync(game.AppId);
                if (fresh != null)
                {
                    fresh.AppId = game.AppId;
                    RestoreLocalIconPaths(fresh);

                    var unlocked = fresh.Achievements.Where(a => a.Unlocked).ToList();
                    var locked = fresh.Achievements.Where(a => !a.Unlocked).ToList();
                    await LibraryCacheService.SaveAchievementsAsync(steamId, game.AppId, locked, unlocked);
                    await GameDetailCacheService.SaveAsync(fresh);

                    Detail = fresh;
                    BuildPreviews();
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
        private void OpenAchievements()
        {
            if (Game == null) return;
            OpenSteamUri($"steam://url/GameStatsPage/{Game.AppId}");
        }

        [RelayCommand]
        private void OpenStore()
        {
            if (Game == null) return;
            OpenSteamUri($"steam://store/{Game.AppId}");
        }

        [RelayCommand]
        private void LaunchOrInstall()
        {
            if (Game == null) return;

            if (NeedsUpdate)
            {
                var res = MessageBox.Show($"Do you want to update {Game.Name}?",
                    "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                    OpenSteamUri($"steam://update/{Game.AppId}");
                return;
            }

            if (!IsInstalled)
            {
                var res = MessageBox.Show($"Do you want to install {Game.Name}?",
                    "Install Game", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                    OpenSteamUri($"steam://install/{Game.AppId}");
                return;
            }

            OpenSteamUri($"steam://run/{Game.AppId}");
        }
        // Steam change detection — refresh LastPlayed when user plays something
        private void OnSteamLibraryChanged()
        {
            if (Game == null || Detail == null) return;

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    Detail.LastPlayed = LastPlayedCacheService.Get(Game.AppId);
                    OnPropertyChanged(nameof(Detail));
                    Debug.WriteLine($"[LibraryGame] LastPlayed refreshed for {Game.AppId}");
                }
                catch { }
            });
        }
        // Achievement unlocked while game is running (requires RunCallbacks timer)
        private void OnAchievementUnlocked(int appId, string apiName)
        {
            if (Game?.AppId != appId) return;

            Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                Debug.WriteLine($"[LibraryGame] Achievement unlocked: {apiName} — refreshing.");
                await LoadAsync(Game);
            });
        }

        // ─── BACKGROUND REFRESH ───────────────────────────────────────────
        private async Task RefreshInBackgroundAsync(int appId)
        {
            try
            {
                await Task.Delay(2000);
                var fresh = await LoadFromSteamworksAsync(appId);
                if (fresh == null || Game?.AppId != appId) return;

                fresh.AppId = appId;
                RestoreLocalIconPaths(fresh);

                var steamId = SessionManager.SteamId64;
                var unlocked = fresh.Achievements.Where(a => a.Unlocked).ToList();
                var locked = fresh.Achievements.Where(a => !a.Unlocked).ToList();
                await LibraryCacheService.SaveAchievementsAsync(steamId, appId, locked, unlocked);
                await GameDetailCacheService.SaveAsync(fresh);

                Detail = fresh;
                BuildPreviews();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibraryGame] Background refresh error: {ex.Message}");
            }
        }

        // ─── STEAMWORKS LOAD ──────────────────────────────────────────────
        private static async Task<GameDetailModel?> LoadFromSteamworksAsync(int appId)
        {
            if (!SteamworksService.IsInitialized)
            {
                Debug.WriteLine($"[LibraryGame] Steamworks not initialized for {appId}");
                return null;
            }
            return await SteamworksService.GetAchievementsForAppAsync(appId);
        }

        // ─── BUILD PREVIEWS ───────────────────────────────────────────────
        private void BuildPreviews()
        {
            UnlockedPreview.Clear();
            LockedPreview.Clear();
            RecentActivity.Clear();

            if (Detail == null) return;

            var unlocked = Detail.Achievements.Where(a => a.Unlocked).ToList();
            var locked = Detail.Achievements.Where(a => !a.Unlocked).ToList();

            bool isFirst = true;
            foreach (var a in unlocked.Take(5))
            {
                a.IsFirst = isFirst;
                isFirst = false;
                UnlockedPreview.Add(a);
            }

            foreach (var a in locked.Take(5))
                LockedPreview.Add(a);

            UnlockedRemaining = Math.Max(0, unlocked.Count - 5);
            LockedRemaining = Math.Max(0, locked.Count - 5);

            OnPropertyChanged(nameof(UnlockedRemaining));
            OnPropertyChanged(nameof(LockedRemaining));
            OnPropertyChanged(nameof(HasUnlockedRemaining));
            OnPropertyChanged(nameof(HasLockedRemaining));
            OnPropertyChanged(nameof(HasAchievements));
            OnPropertyChanged(nameof(HasNoAchievements));

            MostRecentAchievement = unlocked
    .Where(a => a.UnlockTime.HasValue)
    .OrderByDescending(a => a.UnlockTime)
    .FirstOrDefault();

            OnPropertyChanged(nameof(MostRecentAchievement));

            // Recent activity — newest first, max 3 groups
            var groups = unlocked
                .Where(a => a.UnlockTime.HasValue)
                .OrderByDescending(a => a.UnlockTime)
                .GroupBy(a => a.UnlockTime!.Value.Date)
                .Take(3)
                .Select(g => new AchievementDateGroup
                {
                    DateLabel = g.Key.Date == DateTime.UtcNow.Date
                        ? "Today"
                        : g.Key.Date == DateTime.UtcNow.Date.AddDays(-1)
                            ? "Yesterday"
                            : g.Key.ToString("MMMM d", CultureInfo.InvariantCulture),
                    Items = g.ToList()
                });

            foreach (var group in groups)
                RecentActivity.Add(group);
        }

        // ─── LOCAL ICON PATHS ─────────────────────────────────────────────
        private static void RestoreLocalIconPaths(GameDetailModel detail)
        {
            foreach (var a in detail.Achievements)
            {
                a.LocalIconPath = SteamworksService.GetLocalIconPath(detail.AppId, a.ApiName, gray: false);
                a.LocalIconGrayPath = SteamworksService.GetLocalIconPath(detail.AppId, a.ApiName, gray: true);
                a.ResetIconImage();
            }
        }

        // ─── HELPERS ──────────────────────────────────────────────────────
        private static void OpenSteamUri(string uri)
        {
            try
            {
                // SteamExe kľúč nemusí existovať — konštruuj cestu z InstallPath
                var key =
                    Microsoft.Win32.Registry.LocalMachine
                        .OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                    ?? Microsoft.Win32.Registry.LocalMachine
                        .OpenSubKey(@"SOFTWARE\Valve\Steam");

                var steamExe =
                    key?.GetValue("SteamExe") as string
                    ?? Path.Combine(key?.GetValue("InstallPath") as string ?? "", "steam.exe");

                Debug.WriteLine($"[Steam] exe path: {steamExe}, exists: {File.Exists(steamExe)}");

                if (!string.IsNullOrEmpty(steamExe) && File.Exists(steamExe))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = steamExe,
                        Arguments = uri,
                        UseShellExecute = false
                    });
                    Debug.WriteLine($"[Steam] Opened via exe: {uri}");
                    return;
                }

                // Fallback
                Process.Start(new ProcessStartInfo
                {
                    FileName = uri,
                    UseShellExecute = true
                });
                Debug.WriteLine($"[Steam] Opened via shell: {uri}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Steam] OpenUri failed: {uri} — {ex.Message}");
            }
        }
        private static BitmapImage? LoadBitmap(string? path)
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
    }
}