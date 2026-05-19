using System;
using System.Collections.Generic;
using System.Text;
using Rift_App.Services;
using Steamworks;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Rift_App.Services
{
    public static class SteamCallbackService
    {
        private static Callback<UserAchievementStored_t>? _achCallback;
        private static Callback<UserStatsReceived_t>? _statsCallback;
        private static FileSystemWatcher? _vdfWatcher;

        // ─── EVENTS ───────────────────────────────────────────────────────

        /// Fired when any game's playtime/lastplayed changes (user played a game).
        public static event Action? LibraryChanged;

        /// Fired when an achievement is stored (game must be running via Steamworks).
        public static event Action<int, string>? AchievementUnlocked;

        // ─── PUBLIC ───────────────────────────────────────────────────────

        public static void Register()
        {
            if (!SteamworksService.IsInitialized) return;

            try
            {
                _achCallback = Callback<UserAchievementStored_t>.Create(OnAchievementStored);
                _statsCallback = Callback<UserStatsReceived_t>.Create(OnStatsReceived);
                Debug.WriteLine("[SteamCallbacks] Steamworks callbacks registered.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SteamCallbacks] Callback register error: {ex.Message}");
            }

            StartVdfWatcher();
        }

        public static void Unregister()
        {
            _achCallback?.Dispose();
            _statsCallback?.Dispose();
            _achCallback = null;
            _statsCallback = null;

            _vdfWatcher?.Dispose();
            _vdfWatcher = null;
        }

        // ─── PRIVATE: STEAMWORKS CALLBACKS ────────────────────────────────
        private static void OnAchievementStored(UserAchievementStored_t data)
        {
            var appId = (int)data.m_nGameID;
            var name = data.m_rgchAchievementName;
            Debug.WriteLine($"[SteamCallbacks] Achievement stored: {name} in {appId}");
            AchievementUnlocked?.Invoke(appId, name);
        }

        private static async void OnStatsReceived(UserStatsReceived_t data)
        {
            var appId = (int)data.m_nGameID;
            Debug.WriteLine($"[SteamCallbacks] Stats received for {appId}");
            await LastPlayedCacheService.RefreshAsync();
            LibraryChanged?.Invoke();
        }

        // ─── PRIVATE: FILESYSTEM WATCHER ──────────────────────────────────
        private static void StartVdfWatcher()
        {
            try
            {
                var steamPath = Microsoft.Win32.Registry.LocalMachine
                    .OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                    ?.GetValue("InstallPath") as string;
                if (string.IsNullOrEmpty(steamPath)) return;

                uint accountId = SteamUser.GetSteamID().GetAccountID().m_AccountID;
                var configDir = Path.Combine(steamPath, "userdata",
                    accountId.ToString(), "config");

                if (!Directory.Exists(configDir)) return;

                _vdfWatcher = new FileSystemWatcher(configDir, "localconfig.vdf")
                {
                    NotifyFilter = NotifyFilters.LastWrite,
                    EnableRaisingEvents = true
                };

                // Debounce — file may be written multiple times in quick succession
                DateTime _lastFire = DateTime.MinValue;
                _vdfWatcher.Changed += async (_, _) =>
                {
                    if ((DateTime.UtcNow - _lastFire).TotalSeconds < 5) return;
                    _lastFire = DateTime.UtcNow;

                    await Task.Delay(1500); // Wait for Steam to finish writing
                    await LastPlayedCacheService.RefreshAsync();
                    LibraryChanged?.Invoke();
                    Debug.WriteLine("[SteamCallbacks] localconfig.vdf changed → Library notified.");
                };

                Debug.WriteLine("[SteamCallbacks] FileSystemWatcher started on localconfig.vdf");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SteamCallbacks] Watcher error: {ex.Message}");
            }
        }
    }
}