using Rift_App.Languages;
using Rift_App.Services;
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
        public DateTime? LastPlayedDate { get; set; }

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


        public string LastPlayedDisplay
        {
            get
            {
                if (!LastPlayedDate.HasValue)
                    return string.Empty;

                var diff = (DateTime.UtcNow.Date - LastPlayedDate.Value.Date).Days;

                return diff switch
                {
                    0 => L.Get("date_last_played_today"),
                    1 => L.Get("date_last_played_yesterday"),
                    _ => string.Format(
                            L.Get("date_last_played_on"),
                            CapitalizeMonth(
                                LastPlayedDate.Value.ToString(
                                    "d MMM", LanguageService.Current)))

                };
            }
        }

        private static string CapitalizeMonth(string s)
        {
            return string.Join(" ",
                s.Split(' ').Select(word =>
                    word.Length > 0 && char.IsLetter(word[0])
                        ? char.ToUpper(word[0]) + word.Substring(1)
                        : word));
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
}