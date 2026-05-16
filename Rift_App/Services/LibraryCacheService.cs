using Newtonsoft.Json;
using Rift_App.Models;
using Rift_App.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public static class LibraryCacheService
{
    private static readonly string BaseFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RiftApp", "library");

    private static readonly string IconFolder = Path.Combine(BaseFolder, "icons");

    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    // ─── PATHS ────────────────────────────────────────────────────────────

    private static string UserFolder(string steamId) =>
        Path.Combine(BaseFolder, steamId, "games");

    private static string GameFile(string steamId, int appId) =>
        Path.Combine(UserFolder(steamId), $"{appId}.json");

    private static string LockedFile(string steamId, int appId) =>
        Path.Combine(UserFolder(steamId), $"{appId}_locked.json");

    private static string UnlockedFile(string steamId, int appId) =>
        Path.Combine(UserFolder(steamId), $"{appId}_unlocked.json");

    // ─── LOAD ALL ─────────────────────────────────────────────────────────

    public static async Task<List<GameModel>?> LoadAsync()
    {
        var steamId = SessionManager.SteamId64;
        return await LoadAsync(steamId);
    }

    public static async Task<List<GameModel>?> LoadAsync(string steamId)
    {
        try
        {
            var folder = UserFolder(steamId);
            if (!Directory.Exists(folder)) return null;

            var files = Directory.GetFiles(folder, "*.json")
                .Where(f => !f.Contains("_locked") && !f.Contains("_unlocked"))
                .ToList();

            if (files.Count == 0) return null;

            var games = new List<GameModel>();
            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var game = JsonConvert.DeserializeObject<GameModel>(json);
                    if (game != null) games.Add(game);
                }
                catch { }
            }

            Debug.WriteLine($"[LibraryCache] Loaded {games.Count} games for {steamId}");
            return games.Count > 0 ? games : null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LibraryCache] Load error: {ex.Message}");
            return null;
        }
    }

    // ─── SAVE ALL ─────────────────────────────────────────────────────────

    public static async Task SaveAsync(List<GameModel> games)
    {
        var steamId = SessionManager.SteamId64;
        await SaveAsync(steamId, games);
    }

    public static async Task SaveAsync(string steamId, List<GameModel> games)
    {
        try
        {
            EnsureUserFolder(steamId);
            foreach (var game in games)
                await SaveGameAsync(steamId, game);

            Debug.WriteLine($"[LibraryCache] Saved {games.Count} games for {steamId}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LibraryCache] Save error: {ex.Message}");
        }
    }

    public static async Task SaveGameAsync(string steamId, GameModel game)
    {
        try
        {
            EnsureUserFolder(steamId);
            var json = JsonConvert.SerializeObject(game, Formatting.None);
            await File.WriteAllTextAsync(GameFile(steamId, game.AppId), json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LibraryCache] SaveGame error {game.AppId}: {ex.Message}");
        }
    }

    // ─── ACHIEVEMENTS ─────────────────────────────────────────────────────

    public static async Task SaveAchievementsAsync(string steamId, int appId,
        List<AchievementModel> locked, List<AchievementModel> unlocked)
    {
        try
        {
            EnsureUserFolder(steamId);
            await File.WriteAllTextAsync(LockedFile(steamId, appId),
                JsonConvert.SerializeObject(locked, Formatting.None));
            await File.WriteAllTextAsync(UnlockedFile(steamId, appId),
                JsonConvert.SerializeObject(unlocked, Formatting.None));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LibraryCache] SaveAchievements error {appId}: {ex.Message}");
        }
    }

    public static async Task<(List<AchievementModel> Locked,
                               List<AchievementModel> Unlocked)?> LoadAchievementsAsync(
        string steamId, int appId)
    {
        try
        {
            var lf = LockedFile(steamId, appId);
            var uf = UnlockedFile(steamId, appId);
            if (!File.Exists(lf) || !File.Exists(uf)) return null;

            var locked = JsonConvert.DeserializeObject<List<AchievementModel>>(
                               await File.ReadAllTextAsync(lf)) ?? new();
            var unlocked = JsonConvert.DeserializeObject<List<AchievementModel>>(
                               await File.ReadAllTextAsync(uf)) ?? new();

            return (locked, unlocked);
        }
        catch { return null; }
    }

    // ─── SYNC ─────────────────────────────────────────────────────────────

    public static async Task<(List<GameModel> Games, bool Changed)> SyncAsync(
        List<GameModel> cached, List<GameModel> freshFromApi)
    {
        bool changed = false;
        var result = new List<GameModel>(cached);
        var steamId = SessionManager.SteamId64;

        var cachedById = cached.ToDictionary(g => g.AppId);
        var freshById = freshFromApi.ToDictionary(g => g.AppId);

        // New games
        foreach (var fresh in freshFromApi)
        {
            if (cachedById.ContainsKey(fresh.AppId)) continue;
            fresh.IconPath = await DownloadIconAsync(fresh.AppId, fresh.IconUrl);
            await SaveGameAsync(steamId, fresh);
            result.Add(fresh);
            changed = true;
            Debug.WriteLine($"[LibraryCache] New game: {fresh.Name}");
        }

        // Update playtime
        foreach (var cachedGame in cached)
        {
            if (!freshById.TryGetValue(cachedGame.AppId, out var fresh)) continue;
            if (fresh.PlaytimeMinutes <= cachedGame.PlaytimeMinutes) continue;
            cachedGame.PlaytimeMinutes = fresh.PlaytimeMinutes;
            await SaveGameAsync(steamId, cachedGame);
            changed = true;
        }

        return (result, changed);
    }

    // ─── ICONS ────────────────────────────────────────────────────────────

    public static async Task DownloadAllIconsAsync(List<GameModel> games)
    {
        EnsureIconFolder();
        using var semaphore = new SemaphoreSlim(5, 5);

        var tasks = games.Select(async game =>
        {
            await semaphore.WaitAsync();
            try { game.IconPath = await DownloadIconAsync(game.AppId, game.IconUrl); }
            finally { semaphore.Release(); }
        });

        await Task.WhenAll(tasks);
    }

    public static string GetIconPath(int appId) =>
        Path.Combine(IconFolder, $"{appId}.jpg");

    public static bool CacheExists()
    {
        var steamId = SessionManager.SteamId64;
        return Directory.Exists(UserFolder(steamId)) &&
               Directory.GetFiles(UserFolder(steamId), "*.json").Length > 0;
    }

    // ─── PRIVATE ──────────────────────────────────────────────────────────
    public static async Task<string?> DownloadIconAsync(int appId, string? iconUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(iconUrl)) return null;
            EnsureIconFolder();
            var path = GetIconPath(appId);
            if (File.Exists(path)) return path;
            var bytes = await _http.GetByteArrayAsync(iconUrl);
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LibraryCache] Icon error {appId}: {ex.Message}");
            return null;
        }
    }

    private static void EnsureUserFolder(string steamId)
    {
        var folder = UserFolder(steamId);
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
    }

    private static void EnsureIconFolder()
    {
        if (!Directory.Exists(IconFolder))
            Directory.CreateDirectory(IconFolder);
    }
}