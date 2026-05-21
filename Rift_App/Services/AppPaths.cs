using System;
using System.Collections.Generic;
using System.Text;

using System;
using System.IO;

namespace Rift_App.Services
{
    /// Layout:
    ///   RiftApp\
    ///   ├── device.token
    ///   ├── initialized.flag
    ///   ├── shared\
    ///   │   ├── cache\            (steam_tags, steamspy, blacklist, player_*.json)
    ///   │   ├── image_cache\      (URL-hash bitmaps)
    ///   │   ├── achievement_icons\{appId}\
    ///   │   ├── game_images\      (hero JPGs – same for all users)
    ///   │   └── store\
    ///   │       ├── games\{appId}\
    ///   │       └── lists\
    ///   └── {steamId64}\
    ///       ├── cache\            (lastplayed.json)
    ///       ├── library\
    ///       │   ├── games\        ({appId}.json)
    ///       │   ├── achievements\ ({appId}_locked / _unlocked.json)
    ///       │   └── icons\        ({appId}.jpg)
    ///       ├── details\          (GameDetailCache – per-user achievements)
    ///       ├── wishlist\
    ///       │   ├── list.json
    ///       │   └── games\{appId}.json
    ///       └── account\
    ///           ├── snapshot.json
    ///           └── headers\{appId}_header.jpg
    public static class AppPaths
    {
        public static readonly string Root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RiftApp");

        // ─── ROOT FILES ───────────────────────────────────────────────────
        public static string DeviceToken => Path.Combine(Root, "device.token");
        public static string InitFlag => Path.Combine(Root, "initialized.flag");

        // ─── SHARED ───────────────────────────────────────────────────────
        public static string Shared => Path.Combine(Root, "shared");
        public static string SharedCache => Path.Combine(Shared, "cache");
        public static string ImageCache => Path.Combine(Shared, "image_cache");
        public static string AchievementIcons => Path.Combine(Shared, "achievement_icons");
        public static string GameHeroImages => Path.Combine(Shared, "game_images");
        public static string StoreRoot => Path.Combine(Shared, "store");
        public static string StoreGamesRoot => Path.Combine(StoreRoot, "games");
        public static string StoreLists => Path.Combine(StoreRoot, "lists");

        public static string AchievementIconsForGame(int appId) =>
            Path.Combine(AchievementIcons, appId.ToString());

        public static string StoreGameFolder(int appId) =>
            Path.Combine(StoreGamesRoot, appId.ToString());

        // ─── PER USER ─────────────────────────────────────────────────────
        public static string User(string steamId) => Path.Combine(Root, steamId);
        public static string UserCache(string steamId) => Path.Combine(User(steamId), "cache");

        // Library
        public static string Library(string steamId) => Path.Combine(User(steamId), "library");
        public static string LibraryGames(string steamId) => Path.Combine(Library(steamId), "games");
        public static string LibraryAchievements(string steamId) => Path.Combine(Library(steamId), "achievements");
        public static string LibraryIcons(string steamId) => Path.Combine(Library(steamId), "icons");

        // Game detail (per-user – includes unlock status)
        public static string GameDetails(string steamId) => Path.Combine(User(steamId), "details");

        // Wishlist
        public static string Wishlist(string steamId) => Path.Combine(User(steamId), "wishlist");
        public static string WishlistGames(string steamId) => Path.Combine(Wishlist(steamId), "games");

        // Account
        public static string Account(string steamId) => Path.Combine(User(steamId), "account");
        public static string AccountHeaders(string steamId) => Path.Combine(Account(steamId), "headers");

        // ─── ENSURE ───────────────────────────────────────────────────────
        /// Creates the directory if it doesn't exist and returns the path.
        public static string Ensure(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }
    }
}