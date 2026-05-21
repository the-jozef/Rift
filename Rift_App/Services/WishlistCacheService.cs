using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Rift_App.Models;
using System.Diagnostics;
using System.IO;

namespace Rift_App.Services
{
    public static class WishlistCacheService
    {
        private static string FilePath(string steamId64) =>
            Path.Combine(AppPaths.Ensure(AppPaths.Wishlist(steamId64)), "list.json");

        public static async Task<List<WishlistGameModel>?> LoadAsync(string steamId64)
        {
            try
            {
                var path = FilePath(steamId64);
                if (!File.Exists(path)) return null;
                var json = await File.ReadAllTextAsync(path);
                return JsonConvert.DeserializeObject<List<WishlistGameModel>>(json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WishlistCache] Load error: {ex.Message}");
                return null;
            }
        }

        public static async Task SaveAsync(string steamId64, List<WishlistGameModel> games)
        {
            try
            {
                var json = JsonConvert.SerializeObject(games, Formatting.None);
                await File.WriteAllTextAsync(FilePath(steamId64), json);
                Debug.WriteLine($"[WishlistCache] Saved {games.Count} games.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WishlistCache] Save error: {ex.Message}");
            }
        }

        public static async Task RemoveAsync(string steamId64, int appId)
        {
            try
            {
                var games = await LoadAsync(steamId64);
                if (games == null) return;
                games.RemoveAll(g => g.AppId == appId);
                await SaveAsync(steamId64, games);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WishlistCache] Remove error: {ex.Message}");
            }
        }
    }
}