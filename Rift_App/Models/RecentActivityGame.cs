using Rift_App.Languages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Rift_App.Models
{
    public class RecentActivityGame
    {
        public int AppId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string HeaderImageUrl { get; set; } = string.Empty;
        public int Playtime2Weeks { get; set; }
        public int PlaytimeTotal { get; set; }
        public string Playtime2WeeksDisplay { get; set; } = string.Empty;
        public string PlaytimeTotalDisplay { get; set; } = string.Empty;

        // Filled locally
        public int AchievementsUnlocked { get; set; }
        public int AchievementsTotal { get; set; }
        public string LastPlayedDisplay { get; set; } = string.Empty;

        // Recent unlocked achievement icon URLs (max 5 real icons, NO placeholder)
        public List<RecentAchIcon> RecentIcons { get; set; } = new();

        // How many unlocked achievements are beyond the 5 shown icons
        public int ExtraIconCount { get; set; } = 0;
        public bool HasExtraIcons => ExtraIconCount > 0;

        // ── Display helpers ──────────────────────────────────────────────
        public string AchievementsDisplay =>
            AchievementsTotal > 0 ? $"{AchievementsUnlocked} of {AchievementsTotal}" : "";

        public double AchievementsProgress =>
            AchievementsTotal > 0
                ? (double)AchievementsUnlocked / AchievementsTotal
                : 0;

        public bool HasAchievements => AchievementsTotal > 0;

        public string PlaytimeTotalWithRecord => $"{PlaytimeTotalDisplay} {L.Get("playtime_on_record")}";
    }

    public class RecentAchIcon
    {
        public string IconUrl { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        // Legacy fields — kept for JSON compatibility but no longer used for
        // the "+N" button (that now uses RecentActivityGame.ExtraIconCount)
        public int ExtraCount { get; set; }
        public bool IsExtra => ExtraCount > 0;
    }

    public class RecentActivityResponse
    {
        public double TotalHours { get; set; }
        public List<RecentActivityGame> Games { get; set; } = new();
    }
}