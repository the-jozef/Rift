using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace Rift_App.Models
{
    public class GameDetailModel
    {
        public int AppId { get; set; }
        public DateTime? LastPlayed { get; set; }
        public int AchievementsUnlocked { get; set; }
        public int AchievementsTotal { get; set; }
        public List<AchievementModel> Achievements { get; set; } = new();

        // Saved in JSON — the URL used to download the hero image
        public string? HeroImageUrl { get; set; }

        // Set at runtime after checking local disk — NOT saved in JSON
        [JsonIgnore]
        public string? HeroImagePath { get; set; }

        // Cache timestamp — used to check if detail is still fresh
        public DateTime CachedAt { get; set; } = DateTime.UtcNow;

        // ─── DISPLAY HELPERS ──────────────────────────────────────────────

        public string LastPlayedDisplay
        {
            get
            {
                if (!LastPlayed.HasValue) return "Never";

                var now = DateTime.UtcNow;
                var played = LastPlayed.Value.ToUniversalTime();
                var diff = (now.Date - played.Date).Days;

                return diff switch
                {
                    0 => "Today",
                    1 => "Yesterday",
                    _ when played.Year == now.Year => played.ToString("MMM d", CultureInfo.InvariantCulture),
                    _ => played.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)
                };
            }
        }

        public int AchievementsPercent =>
            AchievementsTotal > 0
                ? (int)((double)AchievementsUnlocked / AchievementsTotal * 100)
                : 0;

        public string AchievementsPercentDisplay =>
            AchievementsTotal > 0 ? $"({AchievementsPercent}%)" : string.Empty;
    }
}