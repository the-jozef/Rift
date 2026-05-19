using System;
using System.Collections.Generic;
using System.Text;

namespace Rift_App.Models
{
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