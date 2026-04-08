using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rift_App.GameModels
{
    public class GameModel
    {
        public string Title { get; set; } = string.Empty;
        public string CapsuleImageUrl { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string? ReasonText { get; set; }       // moze byt null
        public string? DiscountPercent { get; set; }  // moze byt null
        public string? OriginalPrice { get; set; }    // moze byt null
        public List<string> Tags { get; set; } = new();
        public List<string> Screenshots { get; set; } = new();
    }
}
