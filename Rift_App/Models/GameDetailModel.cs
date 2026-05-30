using Newtonsoft.Json;
using Rift_App.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using Rift_App.Languages;

namespace Rift_App.Models
{
    public class GameDetailModel
    {
        public int AppId { get; set; }
        public DateTime? LastPlayed { get; set; }
        public int AchievementsUnlocked { get; set; }
        public int AchievementsTotal { get; set; }
        public List<AchievementModel> Achievements { get; set; } = new();
        public string? HeroImageUrl { get; set; }
        [JsonIgnore]
        public string? HeroImagePath { get; set; }
        // Cache timestamp — used to check if detail is still fresh
        public DateTime CachedAt { get; set; } = DateTime.UtcNow;

        // ─── DISPLAY HELPERS ──────────────────────────────────────────────
        public string LastPlayedDisplay
        {
            get
            {
                if (!LastPlayed.HasValue) return L.Get("date_never");

                var now = DateTime.UtcNow;
                var played = LastPlayed.Value.ToUniversalTime();
                var diff = (now.Date - played.Date).Days;

                return diff switch
                {
                    0 => L.Get("date_today"),
                    1 => L.Get("date_yesterday"),
                    _ when played.Year == now.Year =>
                        CapitalizeFirstLetter(
                            played.ToString("d. MMMM", LanguageService.Current)),
                    _ =>
                        CapitalizeFirstLetter(
                            played.ToString("d. MMMM yyyy", LanguageService.Current))
                };
            }
        }

        private static string CapitalizeFirstLetter(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsLetter(s[i]))
                    return s.Substring(0, i)
                         + char.ToUpper(s[i])
                         + s.Substring(i + 1);
            }
            return s;
        }

        public int AchievementsPercent =>
            AchievementsTotal > 0
                ? (int)((double)AchievementsUnlocked / AchievementsTotal * 100)
                : 0;
        public string AchievementsPercentDisplay =>
            AchievementsTotal > 0 ? $"({AchievementsPercent}%)" : string.Empty;
    }
}