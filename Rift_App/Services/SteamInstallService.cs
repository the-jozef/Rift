using Microsoft.Win32;
using Rift_App.Models;
using Rift_App.Services;
using Steamworks;
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
        private static readonly Dictionary<int, string> KnownFreeGames = new()
{
    { 789210,  "Bleach Brave Souls" },
    { 1276390, "Bloons TD Battles 2" },
    { 1938090, "Call of Duty" },
    { 2694490, "Darkest Days" },
    { 1240440, "Halo Infinite" },
    { 2139460, "Once a Human" },
    { 1222670, "The Sims 4" },
    { 1069680, "World of Sea Battles" },
    { 730,     "Counter-Strike 2" },
    { 570,     "Dota 2" },
    { 578080,  "PUBG: Battlegrounds" },
    { 1172470, "Apex Legends" },
    { 252490,  "Rust" },
    { 346110,  "ARK: Survival Evolved" },
    { 550,     "Left 4 Dead 2" },
    { 440,     "Team Fortress 2" },
    { 230410,  "Warframe" },
    { 304930,  "Unturned" },
    { 359550,  "Rainbow Six Siege" },
    { 1517290, "Battlefield 2042" },
    { 1269260, "Battlefield V" },
    { 1238840, "Battlefield 1" },
    { 1238810, "Battlefield 4" },
    { 394360,  "Hunt: Showdown" },
    { 107410,  "Arma 3" },
    { 255710,  "Cities: Skylines" },
    { 322170,  "Geometry Dash" },
    { 381210,  "Dead by Daylight" },
    { 1623730, "Palworld" },
    { 1145360, "Hades" },
    { 2357570, "Lethal Company" },
    { 2050650, "Hogwarts Legacy" },
    { 1966720, "The Planet Crafter" },
    { 2483980, "Enshrouded" },
    { 1326470, "Sons of the Forest" },
    { 526870,  "Satisfactory" },
    { 644830,  "For the King" },
    { 49520,   "Borderlands 2" },
    { 391540,  "Undertale" },
    { 268910,  "Cuphead" },
    { 1446780, "Monster Hunter Rise" },
    { 218620,  "Payday 2" },
    { 239140,  "Dying Light" },
    { 1426210, "It Takes Two" },
    { 582010,  "Monster Hunter: World" },
    { 646570,  "Slay the Spire" },
    { 1593500, "God of War" },
    { 814380,  "Sekiro: Shadows Die Twice" },
    { 435150,  "Divinity: Original Sin 2" },
    { 242760,  "The Forest" },
    { 306130,  "The Elder Scrolls Online" },
    { 489830,  "Skyrim Special Edition" },
    { 377160,  "Fallout 4" },
    { 367520,  "Hollow Knight" },
    { 1250410, "Disco Elysium" },
    { 2183900, "Starfield" },
    { 271590,  "Grand Theft Auto V" },
    { 1091500, "Cyberpunk 2077" },
    { 1245620, "Elden Ring" },
    { 1174180, "Red Dead Redemption 2" },
    { 2308810, "Armored Core VI" },
    { 2915570, "Gray Zone Warfare" },
    { 2215430, "Dead Island 2" },
    { 1612100, "Ghostwire: Tokyo" },
    { 1151340, "Crusader Kings III" },
    { 1063730, "Remnant: From the Ashes" },
    { 1794680, "Marvel's Spider-Man Remastered" },
    { 1551360, "Forza Horizon 5" },
    { 1281930, "Forza Horizon 4" },
    { 2507950, "Delta Force" },
{ 2807960, "Battlefield 6" },
{ 1201240, "Bleach Brave Souls" },
{ 1548520, "Darkest Days" },
{ 412220,  "DDraceNetwork" },
{ 1599340, "Lost Ark" },
{ 3312020, "Lost in Anomaly" },
{ 1977530, "One Armed Cook" },
{ 2551020, "One Armed Robbery" },
{ 1536610, "OpenTTD" },
{ 1502660, "Untrusted" },
        };

        public static List<GameModel> GetSubscribedFreeGames(Dictionary<int, string> freeGames)
        {
            var result = new List<GameModel>();
            try
            {
                foreach (var kvp in freeGames)
                {
                    try
                    {
                        if (!SteamApps.BIsSubscribedApp(new AppId_t((uint)kvp.Key))) continue;

                        result.Add(new GameModel
                        {
                            AppId = kvp.Key,
                            Name = kvp.Value,
                            HeaderImageUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{kvp.Key}/header.jpg",
                            IconUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{kvp.Key}/capsule_sm_120.jpg"
                        });

                        Debug.WriteLine($"[SteamInstall] Subscribed: {kvp.Key} ({kvp.Value})");
                    }
                    catch { }
                }

                Debug.WriteLine($"[SteamInstall] GetSubscribedFreeGames: {result.Count} owned");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SteamInstall] GetSubscribedFreeGames error: {ex.Message}");
            }

            return result;
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