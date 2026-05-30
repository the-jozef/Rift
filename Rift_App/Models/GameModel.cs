using Rift_App.Languages;
using Rift_App.Services;
using Rift_App.ViewModels;
using System;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rift_App.Models
{
    public class GameModel
    {
        public int AppId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string HeaderImageUrl { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string OriginalPrice { get; set; } = string.Empty;
        public int DiscountPercent { get; set; } = 0;
        public string InstallDir { get; set; } = string.Empty;
        public string Type { get; set; } = "game";  
        public bool HasDiscount => DiscountPercent > 0;
        public string ReviewDesc { get; set; } = string.Empty;
        public bool IsFree =>
            Price?.Equals("Free", StringComparison.OrdinalIgnoreCase) == true ||
            Price?.Equals("Free To Play", StringComparison.OrdinalIgnoreCase) == true;
        public string PriceDisplay
        {
            get
            {
                if (Price?.Equals("Free To Play", StringComparison.OrdinalIgnoreCase) == true
                 || Price?.Equals("Free", StringComparison.OrdinalIgnoreCase) == true)
                    return L.Get("store_free_to_play");

                return Price ?? string.Empty;
            }
        }

        public List<string> Genres { get; set; } = new();
        public string GenresDisplay => string.Join(", ", Genres);

        public List<string> TranslatedGenres =>
    Genres.Select(TagTranslationService.Translate).ToList();
        public string TranslatedGenresDisplay =>
            string.Join(", ", TranslatedGenres);

        public List<string> FeaturedTags => Genres.Take(5).ToList();

        public List<string> Screenshots { get; set; } = new();
        public string SteamStoreUrl { get; set; } = string.Empty;

        public int PlaytimeMinutes { get; set; } = 0;
        public string PlaytimeDisplay
        {
            get
            {
                if (PlaytimeMinutes <= 0)
                    return L.Get("playtime_never");

                if (PlaytimeMinutes < 60)
                    return $"{PlaytimeMinutes} {L.Get("playtime_mins")}";

                double hours = PlaytimeMinutes / 60.0;
                return hours % 1 == 0
                    ? $"{(int)hours} {L.Get("playtime_hrs")}"
                    : $"{hours:F1} {L.Get("playtime_hrs")}";
            }
        }
        public string ReviewDescDisplay => ReviewDesc switch
        {
            "Very Positive" => L.Get("review_very_positive"),
            "Positive" => L.Get("review_positive"),
            "Mostly Positive" => L.Get("review_mostly_positive"),
            "Mixed" => L.Get("review_mixed"),
            "Mostly Negative" => L.Get("review_mostly_negative"),
            "Negative" => L.Get("review_negative"),
            "Very Negative" => L.Get("review_very_negative"),
            "No Reviews" => L.Get("review_no_reviews"),
            _ => ReviewDesc
        };

        // Set at runtime by SteamInstallService — not saved in JSON
        [Newtonsoft.Json.JsonIgnore]
        public bool IsInstalled { get; set; }

        // Local disk path for icon — set at runtime by LibraryCacheService
        [Newtonsoft.Json.JsonIgnore]
        public string? IconPath { get; set; }

        public string StatusText { get; set; } = "Available Now";
        public string StatusTextDisplay
        {
            get
            {
                return StatusText switch
                {
                    "Available Now" => L.Get("store_available_now"),
                    "Recommended" => L.Get("store_recommended"),
                    _ => StatusText
                };
            }
        }
        public bool IsRecommended { get; set; } = false;

        [Newtonsoft.Json.JsonIgnore]
        private GameImageViewModel? _iconImage;

        [Newtonsoft.Json.JsonIgnore]
        public GameImageViewModel IconImage =>
            _iconImage ??= new GameImageViewModel(IconUrl);

        [Newtonsoft.Json.JsonIgnore]
        private GameImageViewModel? _headerImage;

        [Newtonsoft.Json.JsonIgnore]
        public GameImageViewModel HeaderImage =>
            _headerImage ??= new GameImageViewModel(HeaderImageUrl);
    }
}