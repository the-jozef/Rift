using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Rift_App.Image
{
    public static class ImageLoader
    {
        // Cache — kľúč je URL, hodnota je hotový BitmapImage
        private static readonly ConcurrentDictionary<string, BitmapImage> _cache = new();

        // ── Načíta obrázok — ak je v cache vráti ihneď, inak stiahne ────

        public static async Task<BitmapImage?> LoadAsync(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            // Ak je už v cache — vráť ho hneď
            if (_cache.TryGetValue(url, out var cached))
                return cached;

            try
            {
                using var httpClient = new HttpClient();
                var bytes = await httpClient.GetByteArrayAsync(url);

                // BitmapImage musí byť vytvorený na UI threade
                BitmapImage? image = null;

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    image = new BitmapImage();
                    image.BeginInit();
                    image.StreamSource = new System.IO.MemoryStream(bytes);
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.EndInit();
                    image.Freeze(); // Dôležité — umožní použiť obrázok z iných threadov
                });

                if (image != null)
                    _cache[url] = image;

                return image;
            }
            catch
            {
                return null;
            }
        }

        // ── Vymaže cache konkrétneho hráča (pri prepnutí účtu) ───────────

        public static void ClearCache()
        {
            _cache.Clear();
        }

        // ── Vymaže iba 1 obrázok z cache ─────────────────────────────────

        public static void RemoveFromCache(string url)
        {
            _cache.TryRemove(url, out _);
        }
    }
}