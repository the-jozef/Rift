using Microsoft.Win32;
using Rift_App.Models;
using Rift_App.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Rift_App.Services
{
    public record InstallInfo(bool IsInstalled, bool NeedsUpdate);

    /// <summary>
    /// Checks whether a Steam game is installed or needs an update
    /// by reading local .acf manifest files. No API calls needed.
    /// StateFlags: 4 = installed, 6 = needs update.
    /// </summary>
    public static class SteamInstallService
    {
        private static Dictionary<int, InstallInfo>? _cache;
        private static DateTime _cacheTime = DateTime.MinValue;

        // Re-scan every 30 seconds at most
        private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(30);

        // ─── PUBLIC ───────────────────────────────────────────────────────

        public static InstallInfo GetInfo(int appId)
        {
            RefreshIfNeeded();
            return _cache?.TryGetValue(appId, out var info) == true
                ? info
                : new InstallInfo(false, false);
        }
        public static List<GameModel> GetAllGames()
        {
            RefreshIfNeeded();

            if (_cache == null) return new List<GameModel>();

            var steamPath = GetSteamPath();
            var libraryPaths = steamPath != null
                ? GetLibraryPaths(steamPath)
                : new List<string>();

            // Build a quick lookup: appId → steamappsPath so we can read the acf for name/installdir
            var result = new List<GameModel>();

            foreach (var steamappsPath in libraryPaths)
            {
                if (!Directory.Exists(steamappsPath)) continue;

                foreach (var acf in Directory.GetFiles(steamappsPath, "appmanifest_*.acf"))
                {
                    try
                    {
                        var content = File.ReadAllText(acf);
                        var appId = ParseAcfInt(content, "appid");
                        var flags = ParseAcfInt(content, "StateFlags");

                        if (appId <= 0) continue;

                        // Only include actually installed games
                        if (flags != 4 && flags != 6) continue;

                        var name = ParseAcfString(content, "name");
                        var installDir = ParseAcfString(content, "installdir");

                        result.Add(new GameModel
                        {
                            AppId = appId,
                            Name = !string.IsNullOrEmpty(name) ? name : $"App {appId}",
                            InstallDir = installDir ?? string.Empty,
                            HeaderImageUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg",
                            IconUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/capsule_sm_120.jpg",
                            PlaytimeMinutes = 0
                        });
                    }
                    catch { }
                }
            }

            Debug.WriteLine($"[SteamInstall] GetAllGames: {result.Count} installed games");
            return result;
        }

        public static List<GameModel> GetAllAppsFromLocalConfig()
        {
            var result = new Dictionary<int, GameModel>();
            try
            {
                var steamPath = GetSteamPath();
                if (string.IsNullOrEmpty(steamPath)) return result.Values.ToList();

                var userdataPath = Path.Combine(steamPath, "userdata");
                if (!Directory.Exists(userdataPath)) return result.Values.ToList();

                foreach (var userDir in Directory.GetDirectories(userdataPath))
                {
                    // ─── 1. localconfig.vdf ───────────────────────────────────
                    var localConfig = Path.Combine(userDir, "config", "localconfig.vdf");
                    if (File.Exists(localConfig))
                    {
                        var content = File.ReadAllText(localConfig);
                        var appsSection = FindVdfSection(content, "apps");
                        if (!string.IsNullOrEmpty(appsSection))
                        {
                            var appRegex = new Regex(@"""(\d+)""\s*\{", RegexOptions.Multiline);
                            foreach (Match m in appRegex.Matches(appsSection))
                            {
                                if (!int.TryParse(m.Groups[1].Value, out int appId) || appId <= 0) continue;
                                result[appId] = new GameModel
                                {
                                    AppId = appId,
                                    Name = appId.ToString(),
                                    HeaderImageUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg",
                                    IconUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg"
                                };
                            }
                        }
                    }

                    // ─── 2. SharedConfig.vdf — obsahuje aj zakúpené/free hry ─
                    var sharedConfig = Path.Combine(userDir, "config", "sharedconfig.vdf");
                    if (File.Exists(sharedConfig))
                    {
                        var content = File.ReadAllText(sharedConfig);
                        var appsSection = FindVdfSection(content, "apps");
                        if (!string.IsNullOrEmpty(appsSection))
                        {
                            var appRegex = new Regex(@"""(\d+)""\s*\{", RegexOptions.Multiline);
                            foreach (Match m in appRegex.Matches(appsSection))
                            {
                                if (!int.TryParse(m.Groups[1].Value, out int appId) || appId <= 0) continue;
                                if (!result.ContainsKey(appId))
                                    result[appId] = new GameModel
                                    {
                                        AppId = appId,
                                        Name = appId.ToString(),
                                        HeaderImageUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg",
                                        IconUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg"
                                    };
                            }
                        }
                    }

                    Debug.WriteLine($"[SteamInstall] localconfig+sharedconfig apps: {result.Count}");
                    break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SteamInstall] localconfig error: {ex.Message}");
            }
            return result.Values.ToList();
        }

        public static List<GameModel> GetAllAppsFromRegistry()
        {
            var result = new Dictionary<int, GameModel>();
            try
            {
                var appsKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\Apps");
                if (appsKey == null)
                {
                    Debug.WriteLine("[Registry] Steam Apps key not found");
                    return result.Values.ToList();
                }

                foreach (var subKeyName in appsKey.GetSubKeyNames())
                {
                    if (!int.TryParse(subKeyName, out int appId) || appId <= 0) continue;

                    using var appKey = appsKey.OpenSubKey(subKeyName);
                    if (appKey == null) continue;

                    // Každá hra ktorú Steam pozná má tu záznam
                    var name = appKey.GetValue("Name") as string ?? string.Empty;

                    result[appId] = new GameModel
                    {
                        AppId = appId,
                        Name = !string.IsNullOrEmpty(name) ? name : appId.ToString(),
                        HeaderImageUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg",
                        IconUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg"
                    };
                }

                Debug.WriteLine($"[Registry] Found {result.Count} apps");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Registry] Error: {ex.Message}");
            }
            return result.Values.ToList();
        }

        // ─── PRIVATE ──────────────────────────────────────────────────────

        private static void RefreshIfNeeded()
        {
            if (_cache != null && DateTime.UtcNow - _cacheTime < ScanInterval) return;
            _cache = ScanAll();
            _cacheTime = DateTime.UtcNow;
        }

        private static Dictionary<int, InstallInfo> ScanAll()
        {
            var result = new Dictionary<int, InstallInfo>();
            var steamPath = GetSteamPath();
            if (string.IsNullOrEmpty(steamPath)) return result;

            foreach (var libPath in GetLibraryPaths(steamPath))
                ScanLibrary(libPath, result);

            Debug.WriteLine($"[SteamInstall] Found {result.Count} games.");
            return result;
        }

        private static string? GetSteamPath()
        {
            try
            {
                // Try 64-bit registry first, then 32-bit
                var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                       ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
                return key?.GetValue("InstallPath") as string;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SteamInstall] Registry error: {ex.Message}");
                return null;
            }
        }

        private static List<string> GetLibraryPaths(string steamPath)
        {
            var paths = new List<string> { Path.Combine(steamPath, "steamapps") };

            try
            {
                var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(vdf)) return paths;

                var content = File.ReadAllText(vdf);

                // Match "path" entries — handles both old and new VDF formats
                foreach (Match m in Regex.Matches(content, @"""path""\s+""([^""]+)"""))
                {
                    var folder = m.Groups[1].Value.Replace(@"\\", @"\");
                    var apps = Path.Combine(folder, "steamapps");
                    if (Directory.Exists(apps) && !paths.Contains(apps))
                        paths.Add(apps);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SteamInstall] VDF parse error: {ex.Message}");
            }

            return paths;
        }

        private static void ScanLibrary(string steamappsPath, Dictionary<int, InstallInfo> result)
        {
            if (!Directory.Exists(steamappsPath)) return;

            try
            {
                foreach (var acf in Directory.GetFiles(steamappsPath, "appmanifest_*.acf"))
                {
                    try
                    {
                        var content = File.ReadAllText(acf);
                        var appId = ParseAcfInt(content, "appid");
                        var flags = ParseAcfInt(content, "StateFlags");

                        if (appId <= 0) continue;

                        // StateFlags 4 = fully installed, 6 = needs update
                        result[appId] = new InstallInfo(
                            IsInstalled: flags == 4 || flags == 6,
                            NeedsUpdate: flags == 6);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SteamInstall] Scan error in {steamappsPath}: {ex.Message}");
            }
        }

        private static int ParseAcfInt(string content, string key)
        {
            var match = Regex.Match(content, $@"""{key}""\s+""(\d+)""");
            return match.Success && int.TryParse(match.Groups[1].Value, out int val) ? val : 0;
        }
        private static string? ParseAcfString(string content, string key)
        {
            var match = Regex.Match(content, $@"""{key}""\s+""([^""]*)""");
            return match.Success ? match.Groups[1].Value : null;
        }
        private static string FindVdfSection(string content, string key)
        {
            var searchKey = $"\"{key}\"";
            int idx = 0;

            while (idx < content.Length)
            {
                var found = content.IndexOf(searchKey, idx, StringComparison.OrdinalIgnoreCase);
                if (found < 0) return string.Empty;

                int afterKey = found + searchKey.Length;
                int j = afterKey;
                while (j < content.Length && (content[j] == ' ' || content[j] == '\t' || content[j] == '\r' || content[j] == '\n')) j++;

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

                idx = found + 1;
            }

            return string.Empty;
        }
    }
}