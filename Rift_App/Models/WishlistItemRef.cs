using System;
using System.Collections.Generic;
using System.Text;

namespace Rift_App.Models
{
    /// <summary>
    /// Jednoduchý odkaz na wishlist hru — len AppId + DateAdded
    /// Vráti ho /wishlist/{steamId}/ids endpoint
    /// </summary>
    public class WishlistItemRef
    {
        public int AppId { get; set; }
        public long DateAdded { get; set; }
    }

    public class WishlistIdsResponse
    {
        public List<WishlistItemRef> Items { get; set; } = new();
    }
}