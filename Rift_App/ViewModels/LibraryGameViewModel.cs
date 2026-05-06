using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Rift_App.Models;
using Rift_App.Services;

namespace Rift_App.ViewModels
{
    public partial class LibraryGameViewModel : ObservableObject
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        private const string BaseUrl = "https://rift-hupv.onrender.com";

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
                // 1. Install status — reads local .acf files, no API call
                var info = SteamInstallService.GetInfo(game.AppId);
                IsInstalled = info.IsInstalled;
                NeedsUpdate = info.NeedsUpdate;
                OnPropertyChanged(nameof(ButtonText));

                // 2. Hero image — disk first, download if missing
                var heroPath = await GameDetailCacheService.EnsureHeroImageAsync(game.AppId);
                HeroImage = LoadBitmapFromPath(heroPath);

                // 3. Detail — 24h disk cache
                var cached = await GameDetailCacheService.LoadAsync(game.AppId);
                if (cached != null)
                {
                    Detail = cached;
                    BuildPreviews();
                    IsLoading = false;

                    // Quietly refresh if cache older than 20h
                    if (DateTime.UtcNow - cached.CachedAt > TimeSpan.FromHours(20))
                        _ = RefreshInBackgroundAsync(game.AppId);

                    return;
                }

                // 4. Cache miss — fetch from backend
                var fresh = await FetchDetailAsync(game.AppId);
                if (fresh != null)
                {
                    fresh.AppId = game.AppId;
                    await GameDetailCacheService.SaveAsync(fresh);
                    Detail = fresh;
                    BuildPreviews();
                }
                else
                {
                    Debug.WriteLine($"[LibraryGame] No detail returned for {game.AppId}");
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

        // ─── BUILD PREVIEWS ───────────────────────────────────────────────

        private void BuildPreviews()
        {
            UnlockedPreview.Clear();
            LockedPreview.Clear();
            RecentActivity.Clear();

            if (Detail == null) return;

            var unlocked = Detail.Achievements.Where(a => a.Unlocked).ToList();
            var locked = Detail.Achievements.Where(a => !a.Unlocked).ToList();

            Debug.WriteLine($"[LibraryGame] Unlocked: {unlocked.Count}, Locked: {locked.Count}");

            // Mark first unlocked so XAML shows Name + Description only for it
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

            // Group unlocked by unlock date — newest first, max 3 groups
            var groups = unlocked
                .Where(a => a.UnlockTime.HasValue)
                .OrderByDescending(a => a.UnlockTime)
                .GroupBy(a => a.UnlockTime!.Value.Date)
                .Take(3)
                .Select(g => new AchievementDateGroup
                {
                    DateLabel = g.Key.ToString("MMMM d"),
                    Items = g.ToList()
                });

            foreach (var group in groups) RecentActivity.Add(group);
        }

        // ─── FETCH FROM BACKEND ───────────────────────────────────────────

        private async Task<GameDetailModel?> FetchDetailAsync(int appId)
        {
            try
            {
                var url = $"{BaseUrl}/api/gamedetail/{SessionManager.SteamId64}/{appId}";
                Debug.WriteLine($"[LibraryGame] Fetching: {url}");

                var json = await _http.GetStringAsync(url);
                Debug.WriteLine($"[LibraryGame] Response length: {json.Length}");

                var result = JsonConvert.DeserializeObject<GameDetailModel>(json);
                Debug.WriteLine($"[LibraryGame] Deserialized — LastPlayed: {result?.LastPlayed}, Achievements: {result?.Achievements?.Count}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibraryGame] Fetch error: {ex.Message}");
                return null;
            }
        }

        private async Task RefreshInBackgroundAsync(int appId)
        {
            try
            {
                var fresh = await FetchDetailAsync(appId);
                if (fresh == null) return;

                fresh.AppId = appId;
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