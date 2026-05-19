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
    public static class ImageCacheService
    {
        private static readonly string DiskCacheFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RiftApp", "image_cache");

        private static readonly ConcurrentDictionary<string, BitmapImage?> _memCache = new();
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        static ImageCacheService()
        {
            if (!Directory.Exists(DiskCacheFolder))
                Directory.CreateDirectory(DiskCacheFolder);
        }

        public static async Task<BitmapImage?> GetAsync(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            // Memory cache
            if (_memCache.TryGetValue(url, out var cached)) return cached;

            var semaphore = _locks.GetOrAdd(url, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();

            try
            {
                if (_memCache.TryGetValue(url, out cached)) return cached;

                BitmapImage? image;

                if (url.StartsWith("C:\\") || url.StartsWith("/") || File.Exists(url))
                {
                    image = LoadFromDisk(url);
                }
                else
                {
                    // Disk cache pre URL
                    var diskPath = GetDiskPath(url);
                    if (File.Exists(diskPath))
                        image = LoadFromDisk(diskPath);
                    else
                        image = await DownloadAsync(url, diskPath);
                }

                _memCache[url] = image;
                return image;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public static BitmapImage? GetIfCached(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            _memCache.TryGetValue(url, out var result);
            return result;
        }

        public static int Count => _memCache.Count;

        // ─── PRIVATE ──────────────────────────────────────────────────────
        private static string GetDiskPath(string url)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(url));
            var hashStr = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()[..16];
            var ext = url.EndsWith(".png") ? ".png" : ".jpg";
            return Path.Combine(DiskCacheFolder, hashStr + ext);
        }

        private static BitmapImage? LoadFromDisk(string path)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch { return null; }
        }
        private static async Task<BitmapImage?> DownloadAsync(string url, string savePath)
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(savePath, bytes);

                var bitmap = new BitmapImage();
                using var stream = new MemoryStream(bytes);
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();

                Debug.WriteLine($"[ImageCache] Downloaded: {url}");
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