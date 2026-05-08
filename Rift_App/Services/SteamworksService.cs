using Rift_App.Models;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace Rift_App.Services
{
    /// <summary>
    /// Wraps Steamworks.NET — reads data directly from the local Steam client.
    /// No API key, no rate limits, no privacy restrictions.
    /// Steam must be running when this service is first used.
    /// </summary>
    public static class SteamworksService
    {
        private static bool _initialized = false;
        private static bool _steamWasAlreadyRunning = false;
        private static readonly SemaphoreSlim _reinitLock = new SemaphoreSlim(1, 1);

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        /// <summary>
        /// Gets correct achievement icon URLs from Steam Web API.
        /// Returns dictionary: apiName -> (iconUrl, iconGrayUrl)
        /// </summary>
        private static async Task<Dictionary<string, (string icon, string iconGray)>> GetIconUrlsFromWebApiAsync(int appId)
        {
            try
            {
                var url = $"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?appid={appId}";
                var json = await _http.GetStringAsync(url);
                var result = new Dictionary<string, (string, string)>();

                var doc = System.Text.Json.JsonDocument.Parse(json);
                var achievements = doc.RootElement
                    .GetProperty("game")
                    .GetProperty("availableGameStats")
                    .GetProperty("achievements")
                    .EnumerateArray();

                foreach (var ach in achievements)
                {
                    var name = ach.GetProperty("name").GetString() ?? "";
                    var icon = ach.TryGetProperty("icon", out var iconProp) ? iconProp.GetString() ?? "" : "";
                    var iconGray = ach.TryGetProperty("icongray", out var grayProp) ? grayProp.GetString() ?? "" : "";
                    result[name] = (icon, iconGray);
                }

                Debug.WriteLine($"[Steamworks] WebAPI icons loaded for {appId}: {result.Count}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Steamworks] WebAPI icon error: {ex.Message}");
                return new Dictionary<string, (string, string)>();
            }
        }

        // ─── INIT ─────────────────────────────────────────────────────────
        public static bool Initialize()
        {
            if (_initialized) return true;
            try
            {
                Environment.SetEnvironmentVariable("SteamAppId", "480");
                Environment.SetEnvironmentVariable("SteamGameId", "480");

                Debug.WriteLine($"[Steamworks] SteamAppId env: {Environment.GetEnvironmentVariable("SteamAppId")}");
                Debug.WriteLine($"[Steamworks] Calling SteamAPI.Init()...");

                _initialized = SteamAPI.Init();
                Debug.WriteLine($"[Steamworks] Init result: {_initialized}");
                return _initialized;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Steamworks] Init error: {ex.Message}");
                return false;
            }
        }

        public static void Shutdown()
        {
            if (!_initialized) return;
            try
            {
                SteamAPI.Shutdown();
                _initialized = false;
                Debug.WriteLine("[Steamworks] Shutdown.");
            }
            catch { }
        }

        public static bool IsInitialized => _initialized;

        // ─── STEAM PROCESS MANAGEMENT ─────────────────────────────────────

        /// <summary>
        /// Returns true if Steam.exe is currently running.
        /// </summary>
        public static bool IsSteamRunning() =>
            Process.GetProcessesByName("steam").Length > 0;

        /// <summary>
        /// Returns true if Steam is installed (checks registry).
        /// </summary>
        public static bool IsSteamInstalled()
        {
            try
            {
                var key = Microsoft.Win32.Registry.LocalMachine
                    .OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                    ?? Microsoft.Win32.Registry.LocalMachine
                    .OpenSubKey(@"SOFTWARE\Valve\Steam");
                return key?.GetValue("InstallPath") != null;
            }
            catch { return false; }
        }

        /// <summary>
        /// Launches Steam and waits until it is fully running (max 30s).
        /// Remembers if Steam was already running before launch.
        /// </summary>
        public static async Task<bool> LaunchSteamAndWaitAsync()
        {
            _steamWasAlreadyRunning = IsSteamRunning();
            if (_steamWasAlreadyRunning) return true;

            try
            {
                var key = Microsoft.Win32.Registry.LocalMachine
                    .OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                    ?? Microsoft.Win32.Registry.LocalMachine
                    .OpenSubKey(@"SOFTWARE\Valve\Steam");

                var steamPath = key?.GetValue("SteamExe") as string
                    ?? key?.GetValue("InstallPath") as string;

                if (string.IsNullOrEmpty(steamPath)) return false;

                // Append steam.exe if path is a directory
                if (Directory.Exists(steamPath))
                    steamPath = Path.Combine(steamPath, "steam.exe");

                Process.Start(new ProcessStartInfo
                {
                    FileName = steamPath,
                    UseShellExecute = true
                });

                // Wait up to 30 seconds for Steam to start
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(1000);
                    if (IsSteamRunning()) return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Steamworks] LaunchSteam error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Closes Steam — only if WE launched it (not if it was already running).
        /// </summary>
        public static void CloseSteamIfWeLaunchedIt()
        {
            if (_steamWasAlreadyRunning) return;
            try
            {
                foreach (var p in Process.GetProcessesByName("steam"))
                    p.Kill();
                Debug.WriteLine("[Steamworks] Steam closed.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Steamworks] CloseSteam error: {ex.Message}");
            }
        }

        // ─── PLAYER INFO ──────────────────────────────────────────────────

        /// <summary>
        /// Returns current logged-in Steam user's info.
        /// </summary>
        public static PlayerInfo? GetCurrentPlayer()
        {
            if (!_initialized) return null;
            try
            {
                var steamId = SteamUser.GetSteamID();
                var name = SteamFriends.GetPersonaName();
                var avatarIdx = SteamFriends.GetLargeFriendAvatar(steamId);

                return new PlayerInfo
                {
                    SteamId = steamId.ToString(),
                    Username = name,
                    AvatarUrl = $"https://avatars.steamstatic.com/{GetAvatarHash(steamId)}_full.jpg",
                    ProfileUrl = $"https://steamcommunity.com/profiles/{steamId}"
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Steamworks] GetCurrentPlayer error: {ex.Message}");
                return null;
            }
        }

        // ─── LIBRARY ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns all games owned by the current user with playtime.
        /// </summary>
        public static List<GameModel> GetLibrary()
        {
            var result = SteamInstallService.GetAllGames();
            Debug.WriteLine($"[Steamworks] Library: {result.Count} games");
            return result;
        }

        // ─── ACHIEVEMENTS ─────────────────────────────────────────────────

        /// <summary>
        /// Returns all achievements for a given appId.
        /// Reads directly from local Steam client — no privacy restrictions.
        /// </summary>
        public static async Task<GameDetailModel?> GetAchievementsForAppAsync(int appId)
        {
            if (!_initialized) return null;

            try
            {
                // Set the AppId environment variable
                Environment.SetEnvironmentVariable("SteamAppId", appId.ToString());

                await Task.Delay(100);
                SteamAPI.RunCallbacks();

                var result = await ReadAchievementsInternalAsync(appId);
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Steamworks] GetAchievementsForApp error: {ex.Message}");
                return null;
            }
            finally
            {
                Environment.SetEnvironmentVariable("SteamAppId", "480");
            }
        }

        private static async Task<GameDetailModel?> ReadAchievementsInternalAsync(int appId)
        {

            var steamPath = Microsoft.Win32.Registry.LocalMachine
    .OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
    ?.GetValue("InstallPath") as string;

            // Skontroluj userdata
            var userDataPath = Path.Combine(steamPath ?? "", "userdata");
            if (Directory.Exists(userDataPath))
            {
                foreach (var userDir in Directory.GetDirectories(userDataPath))
                {
                    Debug.WriteLine($"[Steamworks] UserDir: {userDir}");
                    foreach (var appDir in Directory.GetDirectories(userDir))
                        Debug.WriteLine($"[Steamworks] AppDir: {appDir}");
                }
            }

            // Skontroluj appcache
            var appcachePath = Path.Combine(steamPath ?? "", "appcache");
            if (Directory.Exists(appcachePath))
            {
                foreach (var dir in Directory.GetDirectories(appcachePath))
                    Debug.WriteLine($"[Steamworks] Appcache dir: {dir}");
            }

            // Skontroluj appcache\stats
            var statsPath = Path.Combine(steamPath ?? "", "appcache", "stats");
            if (Directory.Exists(statsPath))
            {
                foreach (var dir in Directory.GetDirectories(statsPath))
                    Debug.WriteLine($"[Steamworks] Stats dir: {dir}");

                // Hľadaj súbory pre appId 289650
                var appStatsPath = Path.Combine(statsPath, $"UserGameStatsSchema_{appId}.bin");
                Debug.WriteLine($"[Steamworks] Stats file exists: {File.Exists(appStatsPath)}");

                // Vypíš všetky súbory v stats
                foreach (var f in Directory.GetFiles(statsPath))
                    Debug.WriteLine($"[Steamworks] Stats file: {Path.GetFileName(f)}");
            }


            var detail = new GameDetailModel { AppId = appId };

            bool statsOk = SteamUserStats.RequestCurrentStats();
            if (!statsOk)
            {
                Debug.WriteLine($"[Steamworks] No stats for {appId}");
                return detail;
            }

            await Task.Delay(500);
            SteamAPI.RunCallbacks();

            uint achCount = SteamUserStats.GetNumAchievements();
            Debug.WriteLine($"[Steamworks] {achCount} achievements for {appId}");

            var iconUrls = await GetIconUrlsFromWebApiAsync(appId);

            var achievements = new List<AchievementModel>();
            int unlocked = 0;

            for (uint i = 0; i < achCount; i++)
            {
                try
                {
                    var apiName = SteamUserStats.GetAchievementName(i);
                    var displayName = SteamUserStats.GetAchievementDisplayAttribute(apiName, "name");
                    var description = SteamUserStats.GetAchievementDisplayAttribute(apiName, "desc");

                    SteamUserStats.GetAchievementAndUnlockTime(apiName, out bool achieved, out uint unlockTime);
                    if (achieved) unlocked++;

                    // Použi Web API URL ak existuje, inak Steamworks fallback
                    iconUrls.TryGetValue(apiName, out var urls);
                    var iconUrl = urls.icon ?? string.Empty;
                    var iconGrayUrl = urls.iconGray ?? string.Empty;

                    Debug.WriteLine($"[Steamworks] {apiName} — icon: '{iconUrl}'");

                    achievements.Add(new AchievementModel
                    {
                        ApiName = apiName,
                        Name = displayName,
                        Description = description,
                        IconUrl = iconUrl,
                        IconGrayUrl = iconGrayUrl,
                        Unlocked = achieved,
                        UnlockTime = unlockTime > 0
                            ? DateTimeOffset.FromUnixTimeSeconds(unlockTime).UtcDateTime
                            : null
                    });
                }
                catch { }
            }

            detail.Achievements = achievements;
            detail.AchievementsUnlocked = unlocked;
            detail.AchievementsTotal = achievements.Count;

            // Download icons to disk
            await DownloadAchievementIconsAsync(appId, achievements);

            // Set local paths after download
            foreach (var a in achievements)
            {
                a.LocalIconPath = GetLocalIconPath(appId, a.ApiName, gray: false);
                a.LocalIconGrayPath = GetLocalIconPath(appId, a.ApiName, gray: true);
                a.ResetIconImage();
            }

            return detail;
        }

        // ─── INSTALL STATUS ───────────────────────────────────────────────

        public static InstallInfo GetInstallInfo(int appId)
        {
            if (!_initialized)
                return SteamInstallService.GetInfo(appId); // fallback to .acf reader

            try
            {
                var aid = new AppId_t((uint)appId);
                bool installed = SteamApps.BIsAppInstalled(aid);
                bool needsUpdate = SteamApps.BIsAppInstalled(aid) &&
                                   SteamApps.GetAppBuildId() != SteamApps.GetAppBuildId();
                return new InstallInfo(installed, false);
            }
            catch
            {
                return SteamInstallService.GetInfo(appId);
            }
        }

        // ─── ICON DOWNLOAD — saves achievement icons to disk ──────────────

        private static async Task DownloadAchievementIconsAsync(int appId, List<AchievementModel> achievements)
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RiftApp", "achievement_icons", appId.ToString());

            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            using var semaphore = new SemaphoreSlim(8, 8); // 8 parallel downloads

            var tasks = achievements.Select(async a =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var iconFile = Path.Combine(folder, $"{a.ApiName}.jpg");
                    var iconGrayFile = Path.Combine(folder, $"{a.ApiName}_gray.jpg");

                    // Download colored icon
                    if (!File.Exists(iconFile))
                    {
                        var bytes = await TryDownloadAsync(a.IconUrl);
                        // If CDN URL failed, try alternate format
                        if (bytes == null)
                            bytes = await TryDownloadAsync(
                                $"https://cdn.akamai.steamstatic.com/steamcommunity/public/images/apps/{appId}/{a.ApiName}.jpg");
                        if (bytes != null)
                            await File.WriteAllBytesAsync(iconFile, bytes);
                    }

                    // Download gray icon
                    if (!File.Exists(iconGrayFile))
                    {
                        var bytes = await TryDownloadAsync(a.IconGrayUrl);
                        if (bytes == null)
                            bytes = await TryDownloadAsync(
                                $"https://cdn.akamai.steamstatic.com/steamcommunity/public/images/apps/{appId}/{a.ApiName}_lock.jpg");
                        if (bytes != null)
                            await File.WriteAllBytesAsync(iconGrayFile, bytes);
                    }
                }
                catch { }
                finally { semaphore.Release(); }
            });

            await Task.WhenAll(tasks);
            Debug.WriteLine($"[Steamworks] Icons downloaded for {appId}");
        }

        /// <summary>
        /// Returns local disk path for an achievement icon if it exists.
        /// </summary>
        public static string? GetLocalIconPath(int appId, string apiName, bool gray = false)
        {
            var suffix = gray ? "_gray" : "";
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RiftApp", "achievement_icons", appId.ToString(), $"{apiName}{suffix}.jpg");
            return File.Exists(path) ? path : null;
        }

        // ─── HELPERS ──────────────────────────────────────────────────────

        private static async Task<byte[]?> TryDownloadAsync(string url)
        {
            try
            {
                var response = await _http.GetAsync(url);
                return response.IsSuccessStatusCode
                    ? await response.Content.ReadAsByteArrayAsync()
                    : null;
            }
            catch { return null; }
        }

        private static string GetAvatarHash(CSteamID steamId)
        {
            // Returns persona name as fallback — real avatar hash needs callback
            // For now use the steam64 in the URL pattern
            return steamId.m_SteamID.ToString();
        }
    }
}