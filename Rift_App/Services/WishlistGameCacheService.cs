using Newtonsoft.Json;
using Rift_App.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Controls;

namespace Rift_App.Services
{
    public static class WishlistGameCacheService
    {
        private static string FilePath(string steamId, int appId) =>
            Path.Combine(AppPaths.Ensure(AppPaths.WishlistGames(steamId)), $"{appId}.json");

        // ─── LOAD ─────────────────────────────────────────────────────────

        public static Task<WishlistGameModel?> LoadAsync(int appId) =>
            LoadAsync(SessionManager.SteamId64, appId);

        public static async Task<WishlistGameModel?> LoadAsync(string steamId, int appId)
        {
            try
            {
                var path = FilePath(steamId, appId);
                if (!File.Exists(path)) return null;

                if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(path)).TotalHours > 24)
                {
                    File.Delete(path);
                    return null;
                }

                var json = await File.ReadAllTextAsync(path);
                return JsonConvert.DeserializeObject<WishlistGameModel>(json);
            }
            catch { return null; }
        }

        // ─── SAVE ─────────────────────────────────────────────────────────

        public static Task SaveAsync(WishlistGameModel game) =>
            SaveAsync(SessionManager.SteamId64, game);

        public static async Task SaveAsync(string steamId, WishlistGameModel game)
        {
            try
            {
                var json = JsonConvert.SerializeObject(game, Formatting.None);
                await File.WriteAllTextAsync(FilePath(steamId, game.AppId), json);
            }
            catch { }
        }

        // ─── DELETE ───────────────────────────────────────────────────────

        public static void Delete(int appId) =>
            Delete(SessionManager.SteamId64, appId);

        public static void Delete(string steamId, int appId)
        {
            try
            {
                var path = FilePath(steamId, appId);
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }
    }
}