using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Rift_App.Services
{
    public static class DeviceTokenService
    {
        private static readonly string FolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RiftApp");
        private static readonly string TokenFilePath = Path.Combine(FolderPath, "device.token");
        private static string? _cachedToken;

        public static string GetOrCreate()
        {
            if (_cachedToken != null) return _cachedToken;

            try
            {
                if (File.Exists(TokenFilePath))
                {
                    _cachedToken = File.ReadAllText(TokenFilePath).Trim();
                    if (Guid.TryParse(_cachedToken, out _)) return _cachedToken;
                }
                return CreateNew();
            }
            catch
            {
                _cachedToken = Guid.NewGuid().ToString();
                return _cachedToken;
            }
        }

        private static string CreateNew()
        {
            _cachedToken = Guid.NewGuid().ToString();
            try
            {
                if (!Directory.Exists(FolderPath)) Directory.CreateDirectory(FolderPath);
                File.WriteAllText(TokenFilePath, _cachedToken);
            }
            catch { }
            return _cachedToken;
        }
    }
}