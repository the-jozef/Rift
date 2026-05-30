using Newtonsoft.Json;
using Rift_App.Languages;
using Rift_App.Services;
using Rift_App.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Rift_App.Models
{
    public class WishlistGameModel
    {
        public int AppId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string HeaderImageUrl { get; set; } = string.Empty;

        // Tags
        public List<string> Tags { get; set; } = new();

        [JsonIgnore]
        public List<string> TranslatedDisplayTags =>
    Tags.Take(5)
        .Select(TagTranslationService.Translate)
        .ToList();

        // Reviews
        public string ReviewDesc { get; set; } = string.Empty;
        public string ReviewCss { get; set; } = string.Empty;

        // Release
        public long ReleaseDateUnix { get; set; }
        public string ReleaseDateDisplay_Raw { get; set; } = string.Empty;
        public string ReleaseDateDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(ReleaseDateDisplay_Raw))
                    return string.Empty;

                if (DateTime.TryParse(ReleaseDateDisplay_Raw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date))
                {
                    return CapitalizeFirstLetter(
                        date.ToString("d. MMMM yyyy", LanguageService.Current));              
                }
                return ReleaseDateDisplay_Raw;
            }
        }
        public bool IsReleased { get; set; }
        public bool IsPreOrder { get; set; }
        public bool IsEarlyAccess { get; set; }
        public bool IsDlc { get; set; }

        // Platform
        public bool PlatformWindows { get; set; } = true;
        public bool PlatformMac { get; set; }

        // Wishlist metadata
        public long DateAddedUnix { get; set; }

        [JsonIgnore]
        public string DateAddedDisplay => DateAddedUnix > 0
    ? string.Format(
        L.Get("wishlist_added_on"),
        DateTimeOffset.FromUnixTimeSeconds(DateAddedUnix)
                      .LocalDateTime
                      .ToString(L.Get("date_format_short")))
    : "";

        // Pricing
        public string Price { get; set; } = string.Empty;
        public string PriceDisplay =>
    Price?.Equals("Free To Play", StringComparison.OrdinalIgnoreCase) == true
        ? L.Get("store_free_to_play")
        : Price;
        public string OriginalPrice { get; set; } = string.Empty;
        public int DiscountPercent { get; set; }
        public bool IsFree { get; set; }

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

        // ─── DISPLAY HELPERS ──────────────────────────────────────────────

        [JsonIgnore]
        public bool HasDiscount => DiscountPercent > 0 && IsReleased;

        [JsonIgnore]
        public bool HasPrice => !string.IsNullOrEmpty(Price) && Price != "N/A";

        [JsonIgnore]
        public bool ShowAddToCart =>
    (IsReleased || IsPreOrder) &&
    !string.IsNullOrEmpty(Price) &&
    Price != "N/A";

        [JsonIgnore]
        public string DiscountDisplay => $"-{DiscountPercent}%";

        [JsonIgnore]
        public System.Windows.Media.SolidColorBrush ReviewBrush
        {
            get
            {
                var hex = ReviewCss switch
                {
                    "veryPositive" => "#66C0F4",
                    "positive" => "#5DA5D0",
                    "mostlyPositive" => "#5DA5D0",
                    "mixed" => "#C6B21E",
                    "mostlyNegative" => "#C83C3C",
                    "negative" => "#C83C3C",
                    _ => "#959BA6"
                };
                return new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
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
        // ─── IMAGE VM ─────────────────────────────────────────────────────

        [JsonIgnore]
        private GameImageViewModel? _headerImage;

        [JsonIgnore]
        public GameImageViewModel HeaderImage =>
            _headerImage ??= new GameImageViewModel(HeaderImageUrl);

        // ─── CONVERSION ───────────────────────────────────────────────────

        public GameModel ToGameModel() => new()
        {
            AppId = AppId,
            Name = Name,
            HeaderImageUrl = HeaderImageUrl,
            Genres = Tags,
            Price = Price,
            OriginalPrice = OriginalPrice,
            DiscountPercent = DiscountPercent
        };
    }
}