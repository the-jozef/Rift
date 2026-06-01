using Rift_App.Models;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace Rift_App.Services
{
    public static class SteamworksService
    {
        private static bool _initialized = false;
        private static bool _steamWasAlreadyRunning = false;

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        private const string BaseUrl = "https://rift-hupv.onrender.com";

        // ─── INIT ─────────────────────────────────────────────────────────

        public static bool Initialize()
        {
            if (_initialized) return true;
            try
            {
                Environment.SetEnvironmentVariable("SteamAppId", "480");
                Environment.SetEnvironmentVariable("SteamGameId", "480");
                _initialized = SteamAPI.Init();
                Debug.WriteLine($"[Steamworks] Init: {_initialized}");

                if (_initialized)
                    _ = LastPlayedCacheService.InitializeAsync();

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
            try { SteamAPI.Shutdown(); _initialized = false; } catch { }
        }

        public static bool IsInitialized => _initialized;

        // ─── STEAM PROCESS ────────────────────────────────────────────────

        public static bool IsSteamRunning() =>
            System.Diagnostics.Process.GetProcessesByName("steam").Length > 0;

        public static bool IsSteamInstalled()
        {
            try
            {
                var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                       ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
                return key?.GetValue("InstallPath") != null;
            }
            catch { return false; }
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        public static async Task<bool> LaunchSteamAndWaitAsync()
        {
            _steamWasAlreadyRunning = IsSteamRunning();
            if (_steamWasAlreadyRunning) return true;
            try
            {
                var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                       ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");

                var steamPath = key?.GetValue("SteamExe") as string
                             ?? key?.GetValue("InstallPath") as string;
                if (string.IsNullOrEmpty(steamPath)) return false;
                if (Directory.Exists(steamPath))
                    steamPath = Path.Combine(steamPath, "steam.exe");

                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    { FileName = steamPath, UseShellExecute = true });

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

        public static void CloseSteamIfWeLaunchedIt()
        {
            if (_steamWasAlreadyRunning) return;
            try
            {
                foreach (var p in System.Diagnostics.Process.GetProcessesByName("steam"))
                    p.Kill();
            }
            catch { }
        }

        // ─── PLAYER INFO ──────────────────────────────────────────────────

        public static PlayerInfo? GetCurrentPlayer()
        {
            if (!_initialized) return null;
            try
            {
                var steamId = SteamUser.GetSteamID();
                var name = SteamFriends.GetPersonaName();
                return new PlayerInfo
                {
                    SteamId = steamId.ToString(),
                    Username = name,
                    AvatarUrl = $"https://avatars.steamstatic.com/{steamId.m_SteamID}_full.jpg",
                    ProfileUrl = $"https://steamcommunity.com/profiles/{steamId}"
                };
            }
            catch { return null; }
        }

        // ─── ACHIEVEMENTS ─────────────────────────────────────────────────

        public static async Task<GameDetailModel?> GetAchievementsForAppAsync(int appId)
        {
            try
            {
                var steamId = SessionManager.SteamId64;
                if (string.IsNullOrEmpty(steamId)) return null;

                await LastPlayedCacheService.InitializeAsync();

                var detail = await ApiService.GetAchievementsAsync(appId, steamId);
                if (detail == null) return null;

                await DownloadAchievementIconsAsync(appId, detail.Achievements);

                foreach (var a in detail.Achievements)
                {
                    a.LocalIconPath = GetLocalIconPath(appId, a.ApiName, gray: false);
                    a.LocalIconGrayPath = GetLocalIconPath(appId, a.ApiName, gray: true);
                    a.ResetIconImage();
                }

                detail.LastPlayed = LastPlayedCacheService.Get(appId);
                return detail;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Steamworks] GetAchievements error: {ex.Message}");
                return null;
            }
        }

        public static Dictionary<int, int> GetPlaytimeMinutes()
        {
            var result = new Dictionary<int, int>();
            try
            {
                if (!_initialized) return result;

                var steamPath = Registry.LocalMachine
                    .OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                    ?.GetValue("InstallPath") as string;
                if (string.IsNullOrEmpty(steamPath)) return result;

                var accountId = SteamUser.GetSteamID().GetAccountID().m_AccountID;
                var localConfig = Path.Combine(steamPath, "userdata",
                    accountId.ToString(), "config", "localconfig.vdf");
                if (!File.Exists(localConfig)) return result;

                var content = File.ReadAllText(localConfig);
                var appsSection = FindVdfSection(content, "apps");
                if (string.IsNullOrEmpty(appsSection)) return result;

                var appRegex = new System.Text.RegularExpressions.Regex(
                    @"""(\d+)""\s*\{([^{}]*(?:\{[^{}]*\}[^{}]*)*)\}",
                    System.Text.RegularExpressions.RegexOptions.Singleline);

                foreach (System.Text.RegularExpressions.Match m in appRegex.Matches(appsSection))
                {
                    if (!int.TryParse(m.Groups[1].Value, out int appId)) continue;
                    var ptMatch = System.Text.RegularExpressions.Regex.Match(
                        m.Groups[2].Value, @"""Playtime""\s+""(\d+)""");
                    if (ptMatch.Success && int.TryParse(ptMatch.Groups[1].Value, out int minutes))
                        result[appId] = minutes;
                }

                Debug.WriteLine($"[Steamworks] Playtime loaded for {result.Count} games");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Steamworks] GetPlaytime error: {ex.Message}");
            }
            return result;
        }

        // ─── INSTALL STATUS ───────────────────────────────────────────────

        public static InstallInfo GetInstallInfo(int appId)
        {
            try
            {
                if (_initialized)
                {
                    var aid = new AppId_t((uint)appId);
                    return new InstallInfo(SteamApps.BIsAppInstalled(aid), false);
                }
            }
            catch { }
            return SteamInstallService.GetInfo(appId);
        }

        // ─── ICON PATHS — shared\achievement_icons\{appId}\ ──────────────

        public static string? GetLocalIconPath(int appId, string apiName, bool gray = false)
        {
            var path = Path.Combine(
                AppPaths.AchievementIconsForGame(appId),
                gray ? $"{apiName}_gray.jpg" : $"{apiName}.jpg");
            return File.Exists(path) ? path : null;
        }

        // ─── PRIVATE ──────────────────────────────────────────────────────

        private static async Task DownloadAchievementIconsAsync(int appId, List<AchievementModel> achievements)
        {
            var folder = AppPaths.Ensure(AppPaths.AchievementIconsForGame(appId));

            using var semaphore = new SemaphoreSlim(8, 8);

            var tasks = achievements.Select(async a =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var iconFile = Path.Combine(folder, $"{a.ApiName}.jpg");
                    var iconGrayFile = Path.Combine(folder, $"{a.ApiName}_gray.jpg");

                    if (!File.Exists(iconFile) && !string.IsNullOrEmpty(a.IconUrl))
                    {
                        var bytes = await TryDownloadAsync(a.IconUrl);
                        if (bytes != null) await File.WriteAllBytesAsync(iconFile, bytes);
                    }

                    if (!File.Exists(iconGrayFile) && !string.IsNullOrEmpty(a.IconGrayUrl))
                    {
                        var bytes = await TryDownloadAsync(a.IconGrayUrl);
                        if (bytes != null) await File.WriteAllBytesAsync(iconGrayFile, bytes);
                    }
                }
                catch { }
                finally { semaphore.Release(); }
            });

            await Task.WhenAll(tasks);
            Debug.WriteLine($"[Steamworks] Icons downloaded for {appId}");
        }

        private static async Task<byte[]?> TryDownloadAsync(string url)
        {
            try
            {
                if (string.IsNullOrEmpty(url)) return null;
                var response = await _http.GetAsync(url);
                return response.IsSuccessStatusCode
                    ? await response.Content.ReadAsByteArrayAsync()
                    : null;
            }
            catch { return null; }
        }

        private static string FindVdfSection(string content, string key)
        {
            var searchKey = $"\"{key}\"";
            int idx = 0;
            while (idx < content.Length)
            {
                var found = content.IndexOf(searchKey, idx, StringComparison.OrdinalIgnoreCase);
                if (found < 0) return string.Empty;

                int j = found + searchKey.Length;
                while (j < content.Length &&
                       (content[j] == ' ' || content[j] == '\t' ||
                        content[j] == '\r' || content[j] == '\n')) j++;

                if (j < content.Length && content[j] == '{')
                {
                    int depth = 1, i = j + 1;
                    while (i < content.Length && depth > 0)
                    {
                        if (content[i] == '{') depth++;
                        else if (content[i] == '}') depth--;
                        i++;
                    }
                    return depth == 0
                        ? content.Substring(j + 1, i - j - 2)
                        : string.Empty;
                }
                idx = found + 1;
            }
            return string.Empty;
        }

        private record SchemaEntry(string ApiName, string DisplayName, string Description, string IconUrl, string IconGrayUrl);
    }
}