using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Rift_App.Models;
using System.Diagnostics;
using System.IO;

namespace Rift_App.Services
{
    /// <summary>
    /// Persists wishlist data locally — no TTL expiry.
    /// Sync happens in background on every open (added/removed games + price updates).
    /// Location: AppData\RiftApp\wishlist\{steamId64}_wishlist.json
    /// </summary>
    public static class WishlistCacheService
    {
        private static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RiftApp", "wishlist");

        private static string FilePath(string steamId64) =>
            Path.Combine(Folder, $"{steamId64}_wishlist.json");

        // ─── LOAD ─────────────────────────────────────────────────────────

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

        // ─── SAVE ─────────────────────────────────────────────────────────

        public static async Task SaveAsync(string steamId64, List<WishlistGameModel> games)
        {
            try
            {
                EnsureFolder();
                var json = JsonConvert.SerializeObject(games, Formatting.None);
                await File.WriteAllTextAsync(FilePath(steamId64), json);
                Debug.WriteLine($"[WishlistCache] Saved {games.Count} games.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WishlistCache] Save error: {ex.Message}");
            }
        }

        // ─── REMOVE SINGLE ────────────────────────────────────────────────

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

        // ─── SYNC ─────────────────────────────────────────────────────────
        // Compares cached vs fresh Steam data.
        // Adds new games, removes deleted ones, updates prices/reviews.
        // DateAddedUnix is preserved from cache for games that already existed.

        public static Task<(List<WishlistGameModel> Games, bool Changed)> SyncAsync(
            List<WishlistGameModel> cached,
            List<WishlistGameModel> fresh)
        {
            bool changed = false;
            var result = new List<WishlistGameModel>(cached);

            var cachedById = cached.ToDictionary(g => g.AppId);
            var freshById = fresh.ToDictionary(g => g.AppId);

            // Add new games (in Steam but not in cache)
            foreach (var g in fresh.Where(g => !cachedById.ContainsKey(g.AppId)))
            {
                result.Add(g);
                changed = true;
                Debug.WriteLine($"[WishlistCache] New game: {g.Name}");
            }

            // Remove games no longer on wishlist
            var toRemove = result.Where(g => !freshById.ContainsKey(g.AppId)).ToList();
            foreach (var g in toRemove)
            {
                result.Remove(g);
                changed = true;
                Debug.WriteLine($"[WishlistCache] Removed game: {g.Name}");
            }

            // Update price / review data for existing games
            foreach (var game in result)
            {
                if (!freshById.TryGetValue(game.AppId, out var freshGame)) continue;

                if (game.Price == freshGame.Price &&
                    game.DiscountPercent == freshGame.DiscountPercent &&
                    game.ReviewDesc == freshGame.ReviewDesc) continue;

                game.Price = freshGame.Price;
                game.OriginalPrice = freshGame.OriginalPrice;
                game.DiscountPercent = freshGame.DiscountPercent;
                game.ReviewDesc = freshGame.ReviewDesc;
                game.ReviewCss = freshGame.ReviewCss;
                game.IsReleased = freshGame.IsReleased;
                game.ReleaseDateDisplay = freshGame.ReleaseDateDisplay;
                changed = true;
                Debug.WriteLine($"[WishlistCache] Updated: {game.Name}");
            }

            return Task.FromResult((result, changed));
        }

        // ─── HELPER ───────────────────────────────────────────────────────

        private static void EnsureFolder()
        {
            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
        }
    }
}