using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Rift_App.Models;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Rift_App.Services
{
    public static class StoreGameCacheService
    {
        private static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RiftApp", "Store", "games");

        private static readonly TimeSpan TTL = TimeSpan.FromHours(24);

        private class Entry<T>
        {
            public DateTime SavedAt { get; set; }
            public T? Data { get; set; }
        }

        // ─── LOAD ─────────────────────────────────────────────────────────

        public static async Task<GameModel?> LoadAsync(int appId)
        {
            try
            {
                var path = Path.Combine(Folder, $"{appId}.json");
                if (!File.Exists(path)) return null;

                var json = await File.ReadAllTextAsync(path);
                var entry = JsonConvert.DeserializeObject<Entry<GameModel>>(json);
                if (entry?.Data == null) return null;

                if (DateTime.UtcNow - entry.SavedAt > TTL)
                {
                    File.Delete(path);
                    return null;
                }

                return entry.Data;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreCache] Load {appId}: {ex.Message}");
                return null;
            }
        }

        // ─── SAVE ─────────────────────────────────────────────────────────

        public static async Task SaveAsync(GameModel game)
        {
            try
            {
                EnsureFolder();
                var entry = new Entry<GameModel> { SavedAt = DateTime.UtcNow, Data = game };
                var json = JsonConvert.SerializeObject(entry, Formatting.None);
                await File.WriteAllTextAsync(Path.Combine(Folder, $"{game.AppId}.json"), json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreCache] Save {game.AppId}: {ex.Message}");
            }
        }

        // ─── BULK LOAD ────────────────────────────────────────────────────
        // Vráti všetky platné (neexpirované) hry z disku — pre cold start
        // Returns all valid (non-expired) games from disk — for cold start

        public static async Task<List<GameModel>> LoadAllAsync()
        {
            var result = new List<GameModel>();
            try
            {
                EnsureFolder();
                foreach (var file in Directory.GetFiles(Folder, "*.json"))
                {
                    var json = await File.ReadAllTextAsync(file);
                    var entry = JsonConvert.DeserializeObject<Entry<GameModel>>(json);
                    if (entry?.Data == null) continue;

                    if (DateTime.UtcNow - entry.SavedAt > TTL)
                    {
                        File.Delete(file);
                        continue;
                    }

                    result.Add(entry.Data);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreCache] LoadAll: {ex.Message}");
            }
            return result;
        }

        // ─── CHECK ────────────────────────────────────────────────────────
        // Rýchla kontrola či je hra cachovaná — bez deserializácie
        // Fast check if game is cached — without full deserialization

        public static bool IsCached(int appId)
        {
            try
            {
                var path = Path.Combine(Folder, $"{appId}.json");
                if (!File.Exists(path)) return false;

                var info = new FileInfo(path);
                return DateTime.UtcNow - info.LastWriteTimeUtc < TTL;
            }
            catch { return false; }
        }

        // ─── INVALIDATE ───────────────────────────────────────────────────

        public static void Invalidate(int appId)
        {
            try
            {
                var path = Path.Combine(Folder, $"{appId}.json");
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        public static void ClearAll()
        {
            try
            {
                if (Directory.Exists(Folder))
                    Directory.Delete(Folder, recursive: true);
            }
            catch { }
        }

        private static void EnsureFolder()
        {
            if (!Directory.Exists(Folder))
                Directory.CreateDirectory(Folder);
        }
    }
}