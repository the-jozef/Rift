using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Rift_App.ViewModels;

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
        public List<string> DisplayTags => Tags.Take(5).ToList();

        // Reviews
        public string ReviewDesc { get; set; } = string.Empty;
        public string ReviewCss { get; set; } = string.Empty;

        // Release
        public long ReleaseDateUnix { get; set; }
        public string ReleaseDateDisplay { get; set; } = string.Empty;
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
           ? DateTimeOffset.FromUnixTimeSeconds(DateAddedUnix)
                    .LocalDateTime
                    .ToString("d/M/yyyy"): "";

        // Pricing
        public string Price { get; set; } = string.Empty;
        public string OriginalPrice { get; set; } = string.Empty;
        public int DiscountPercent { get; set; }
        public bool IsFree { get; set; }

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
                    "overwhelmingPositive" => "#66C0F4",
                    "positive" => "#5DA5D0",
                    "mixed" => "#C6B21E",
                    "negative" => "#C83C3C",
                    "overwhelminglyNegative" => "#C83C3C",
                    _ => "#959BA6"
                };
                return new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
            }
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