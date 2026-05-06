using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        [Newtonsoft.Json.JsonIgnore]
        public string? HeroImagePath { get; set; }

        // Cache timestamp — used to check if detail is still fresh
        public DateTime CachedAt { get; set; } = DateTime.UtcNow;

        // ─── DISPLAY HELPERS ──────────────────────────────────────────────

        public string LastPlayedDisplay =>
            LastPlayed.HasValue ? LastPlayed.Value.ToString("MMM d") : "Unknown";

        public int AchievementsPercent =>
            AchievementsTotal > 0
                ? (int)((double)AchievementsUnlocked / AchievementsTotal * 100)
                : 0;

        public string AchievementsPercentDisplay =>
            $"({AchievementsPercent}%)";
    }
}