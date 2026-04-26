using SteamProxyBackend.Models;
using System.ComponentModel.DataAnnotations;

namespace SteamProxyBackend.Models
{
    public class User
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string SteamId64 { get; set; } = string.Empty;

        // Prepared for future password recovery
        public string? Email { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        //public ICollection<DeviceAccount> DeviceAccounts { get; set; } = new List<DeviceAccount>();
       // public ICollection<LoginHistory> LoginHistories { get; set; } = new List<LoginHistory>();
    }
}