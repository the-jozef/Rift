using SteamProxyBackend.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SteamProxyBackend.Models
{
    public class DeviceAccount
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string DeviceToken { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string LastLocation { get; set; } = "Store";
        public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
        public bool IsLastActive { get; set; } = false;

        // Navigation property — NO [ForeignKey] attribute here
        // Foreign key is configured in AppDbContext.OnModelCreating
        public User? User { get; set; }
    }
}