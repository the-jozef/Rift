using Microsoft.Win32;
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
    }
}