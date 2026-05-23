using System;
using System.Collections.Generic;
using System.Text;

namespace Rift_App.Models
{
    public class SearchResultModel
    {
        public int AppId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string HeaderImageUrl { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string OriginalPrice { get; set; } = string.Empty;
        public int DiscountPercent { get; set; }
        public bool IsFree { get; set; }
        public bool HasDiscount { get; set; }
        public bool IsComingSoon { get; set; }

        // ── Display helpers ──────────────────────────────────────────────
        public string DiscountDisplay => $"-{DiscountPercent}%";

        public string PriceDisplay =>
            IsComingSoon ? "Coming Soon" :
            IsFree ? "Free To Play" :
            string.IsNullOrEmpty(Price) || Price == "N/A"
                              ? "Coming Soon" :   // fallback if backend missed it
            Price;
    }

    public class SearchResponse
    {
        public List<SearchResultModel> Results { get; set; } = new();
    }
}