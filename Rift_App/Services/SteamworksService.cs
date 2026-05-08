using Rift_App.Models;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Text.Json;

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
            Process.GetProcessesByName("steam").Length > 0;

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
                if (Directory.Exists(steamPath))
                    steamPath = Path.Combine(steamPath, "steam.exe");

                Process.Start(new ProcessStartInfo { FileName = steamPath, UseShellExecute = true });

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
            try { foreach (var p in Process.GetProcessesByName("steam")) p.Kill(); } catch { }
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

        // ─── LIBRARY ──────────────────────────────────────────────────────

        public static List<GameModel> GetLibrary() =>
            SteamInstallService.GetAllGames();

        // ─── ACHIEVEMENTS ─────────────────────────────────────────────────
        // Schema (názvy + ikony) z API — funguje vždy
        // Unlock status z Steamworks lokálne — funguje vždy bez ohľadu na privacy

        public static async Task<GameDetailModel?> GetAchievementsForAppAsync(int appId)
        {
            try
            {
                var steamId = SessionManager.SteamId64;
                if (string.IsNullOrEmpty(steamId)) return null;

                var detail = await ApiService.GetAchievementsAsync(appId, steamId);
                if (detail == null) return null;

                // Stiahni ikony na disk
                await DownloadAchievementIconsAsync(appId, detail.Achievements);

                foreach (var a in detail.Achievements)
                {
                    a.LocalIconPath = GetLocalIconPath(appId, a.ApiName, gray: false);
                    a.LocalIconGrayPath = GetLocalIconPath(appId, a.ApiName, gray: true);
                    a.ResetIconImage();
                }

                detail.LastPlayed = GetLastPlayed(appId);
                return detail;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Steamworks] GetAchievements error: {ex.Message}");
                return null;
            }
        }

        private static DateTime? GetLastPlayed(int appId)
        {
            try
            {
                if (!_initialized) return null;
                var steamPath = Microsoft.Win32.Registry.LocalMachine
                    .OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                    ?.GetValue("InstallPath") as string;
                if (string.IsNullOrEmpty(steamPath)) return null;

                var accountId = SteamUser.GetSteamID().GetAccountID().m_AccountID;
                var localConfig = Path.Combine(steamPath, "userdata",
                    accountId.ToString(), "config", "localconfig.vdf");

                if (!File.Exists(localConfig)) return null;

                var content = File.ReadAllText(localConfig);

                // Nájdi apps sekciu
                var appsSection = FindVdfSection(content, "apps");
                if (string.IsNullOrEmpty(appsSection)) return null;

                // Nájdi sekciu pre konkrétny appId
                var appSection = FindVdfSection(appsSection, appId.ToString());
                if (string.IsNullOrEmpty(appSection)) return null;

                // Parsuj LastPlayed
                var match = System.Text.RegularExpressions.Regex.Match(
                    appSection, @"""LastPlayed""\s+""(\d+)""");
                if (!match.Success) return null;

                if (long.TryParse(match.Groups[1].Value, out long timestamp) && timestamp > 0)
                    return DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Steamworks] GetLastPlayed error: {ex.Message}");
            }
            return null;
        }

        // ─── SCHEMA FROM API ──────────────────────────────────────────────

        private record SchemaEntry(string ApiName, string DisplayName, string Description, string IconUrl, string IconGrayUrl);

        private static async Task<List<SchemaEntry>> GetSchemaFromApiAsync(int appId)
        {
            try
            {
                var json = await _http.GetStringAsync($"{BaseUrl}/api/steam/schema/{appId}");
                var doc = JsonDocument.Parse(json);
                var result = new List<SchemaEntry>();

                foreach (var a in doc.RootElement.GetProperty("achievements").EnumerateArray())
                {
                    result.Add(new SchemaEntry(
                        a.GetProperty("apiName").GetString() ?? "",
                        a.GetProperty("displayName").GetString() ?? "",
                        a.GetProperty("description").GetString() ?? "",
                        a.GetProperty("iconUrl").GetString() ?? "",
                        a.GetProperty("iconGrayUrl").GetString() ?? ""
                    ));
                }

                Debug.WriteLine($"[Steamworks] Schema loaded: {result.Count} achievements for {appId}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Steamworks] Schema error: {ex.Message}");
                return new List<SchemaEntry>();
            }
        }

        // ─── UNLOCK STATUS FROM STEAMWORKS ────────────────────────────────
        // Číta priamo z lokálneho Steam cache — funguje pre private profil

        private static Dictionary<string, (bool achieved, uint unlockTime)> GetUnlockStatusFromSteamworks(int appId)
        {
            var result = new Dictionary<string, (bool, uint)>();
            if (!_initialized) return result;

            try
            {
                SteamAPI.RunCallbacks();
                SteamUserStats.RequestCurrentStats();
                Thread.Sleep(500);
                SteamAPI.RunCallbacks();

                uint achCount = SteamUserStats.GetNumAchievements();
                Debug.WriteLine($"[Steamworks] GetNumAchievements: {achCount}");

                for (uint i = 0; i < achCount; i++)
                {
                    var apiName = SteamUserStats.GetAchievementName(i);
                    SteamUserStats.GetAchievementAndUnlockTime(apiName, out bool achieved, out uint unlockTime);
                    result[apiName] = (achieved, unlockTime);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Steamworks] UnlockStatus error: {ex.Message}");
            }

            return result;
        }

        private static string FindVdfSection(string content, string key)
        {
            // Hľadaj presne "key" s úvodzovkami
            var searchKey = $"\"{key}\"";
            int idx = 0;

            while (idx < content.Length)
            {
                var found = content.IndexOf(searchKey, idx, StringComparison.OrdinalIgnoreCase);
                if (found < 0) return string.Empty;

                // Skontroluj že je to samostatný kľúč — pred ním musí byť whitespace alebo začiatok
                int afterKey = found + searchKey.Length;

                // Preskočí whitespace
                int j = afterKey;
                while (j < content.Length && (content[j] == ' ' || content[j] == '\t' || content[j] == '\r' || content[j] == '\n')) j++;

                // Ďalší znak musí byť { — inak to nie je sekcia ale value
                if (j < content.Length && content[j] == '{')
                {
                    int braceStart = j;
                    int depth = 1;
                    int i = braceStart + 1;
                    while (i < content.Length && depth > 0)
                    {
                        if (content[i] == '{') depth++;
                        else if (content[i] == '}') depth--;
                        i++;
                    }
                    return depth == 0 ? content.Substring(braceStart + 1, i - braceStart - 2) : string.Empty;
                }

                // Nie je to sekcia — hľadaj ďalej
                idx = found + 1;
            }

            return string.Empty;
        }

        private static Dictionary<string, (bool achieved, uint unlockTime)> ParseUserGameStats(string filePath)
        {
            var result = new Dictionary<string, (bool, uint)>();

            try
            {
                var bytes = File.ReadAllBytes(filePath);
                // Steam .bin súbor obsahuje achievement názvy a ich stav
                // Formát: hľadáme null-terminated strings s hodnotami
                var text = System.Text.Encoding.UTF8.GetString(bytes);

                // Hľadaj achievement API names — sú to ASCII stringy
                // Followed by unlock byte (1=unlocked) a timestamp
                int i = 0;
                while (i < bytes.Length - 8)
                {
                    // Nájdi začiatok stringu (printable ASCII)
                    if (bytes[i] >= 0x20 && bytes[i] < 0x7F)
                    {
                        // Čítaj string do null byte
                        int start = i;
                        while (i < bytes.Length && bytes[i] != 0) i++;

                        if (i - start >= 3 && i - start <= 64)
                        {
                            var name = System.Text.Encoding.ASCII.GetString(bytes, start, i - start);

                            // Skontroluj či vyzerá ako achievement API name
                            if (IsValidAchievementName(name) && i + 5 < bytes.Length)
                            {
                                bool achieved = bytes[i + 1] == 1;
                                uint unlockTime = 0;
                                if (achieved && i + 5 < bytes.Length)
                                    unlockTime = BitConverter.ToUInt32(bytes, i + 2);

                                result[name] = (achieved, unlockTime);
                            }
                        }
                    }
                    i++;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Steamworks] ParseStats error: {ex.Message}");
            }

            return result;
        }

        private static bool IsValidAchievementName(string name)
        {
            if (name.Length < 3 || name.Length > 64) return false;
            return name.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-');
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

        // ─── ICON DOWNLOAD ────────────────────────────────────────────────

        private static async Task DownloadAchievementIconsAsync(int appId, List<AchievementModel> achievements)
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RiftApp", "achievement_icons", appId.ToString());

            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

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

        public static string? GetLocalIconPath(int appId, string apiName, bool gray = false)
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RiftApp", "achievement_icons", appId.ToString(),
                gray ? $"{apiName}_gray.jpg" : $"{apiName}.jpg");
            return File.Exists(path) ? path : null;
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
    }
}