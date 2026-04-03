using System;
using System.IO;

namespace Rift_App.Services
{
    public static class SessionService
    {
        private static readonly string _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RiftApp", "session.txt");

        public static void Save(string steamId64)
        {
            File.WriteAllText(_path, steamId64);
        }

        public static string? Load()
        {
            if (!File.Exists(_path)) return null;
            string id = File.ReadAllText(_path).Trim();
            return string.IsNullOrEmpty(id) ? null : id;
        }

        public static void Clear()
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }
    }
}
