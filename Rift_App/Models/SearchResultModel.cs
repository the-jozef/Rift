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

        // Display helpers
        public string DiscountDisplay => $"-{DiscountPercent}%";
        public string PriceDisplay => IsFree ? "Free To Play" : Price;
    }
    public class SearchResponse
    {
        public List<SearchResultModel> Results { get; set; } = new();
    }
}