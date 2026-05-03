using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Rift_App.Services
{
    /// <summary>
    /// In-memory image cache — každý URL sa stiahne raz a uloží.
    /// In-memory image cache — each URL is downloaded once and stored.
    /// Ďalšie volania s rovnakým URL vrátia okamžite z pamäte.
    /// Subsequent calls with the same URL return instantly from memory.
    /// </summary>
    public static class ImageCacheService
    {
        // Hlavný cache slovník — main cache dictionary
        private static readonly ConcurrentDictionary<string, BitmapImage?> _cache = new();

        // Zabraňuje dvojitému stiahnutiu toho istého URL
        // Prevents double-downloading the same URL
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        // ─── VEREJNÉ API ──────────────────────────────────────────────────

        /// <summary>
        /// Vráti BitmapImage z cache alebo stiahne a uloží.
        /// Returns BitmapImage from cache or downloads and stores.
        /// </summary>
        public static async Task<BitmapImage?> GetAsync(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            // Ak už je v cache — vráť okamžite
            // If already cached — return immediately
            if (_cache.TryGetValue(url, out var cached))
                return cached;

            // Zabezpeč aby každý URL stiahol len jeden thread
            // Ensure only one thread downloads each URL
            var semaphore = _locks.GetOrAdd(url, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();

            try
            {
                // Skontroluj znovu po čakaní — check again after waiting
                if (_cache.TryGetValue(url, out cached))
                    return cached;

                var image = await DownloadAsync(url);
                _cache[url] = image;
                return image;
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Preloaduje zoznam URL na pozadí.
        /// Preloads a list of URLs in the background.
        /// </summary>
        public static async Task PreloadAsync(IEnumerable<string> urls, int delayBetweenMs = 300)
        {
            foreach (var url in urls)
            {
                if (string.IsNullOrEmpty(url)) continue;
                if (_cache.ContainsKey(url)) continue;

                await GetAsync(url);
                await Task.Delay(delayBetweenMs);
            }
        }

        /// <summary>
        /// Synchronne vráti z cache ak existuje, inak null.
        /// Synchronously returns from cache if exists, otherwise null.
        /// Používa sa v XAML converter — used in XAML converter.
        /// </summary>
        public static BitmapImage? GetIfCached(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            _cache.TryGetValue(url, out var result);
            return result;
        }

        public static int Count => _cache.Count;

        // ─── PRIVATE ──────────────────────────────────────────────────────

        private static async Task<BitmapImage?> DownloadAsync(string url)
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(url);

                // BitmapImage musí byť vytvorený na UI threade — musíme použiť Dispatcher
                // BitmapImage must be created — use MemoryStream + Freeze for thread safety
                var bitmap = new BitmapImage();
                using var stream = new MemoryStream(bytes);

                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze(); // Umožní použitie na inom threade — allows cross-thread use

                Debug.WriteLine($"[ImageCache] Loaded: {url}");
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageCache] Failed: {url} — {ex.Message}");
                return null;
            }
        }
    }
}