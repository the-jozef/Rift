using SteamProxyBackend.Models;
using System.ComponentModel.DataAnnotations;

namespace SteamProxyBackend.Models
{
    public class Device
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string DeviceToken { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<DeviceAccount> DeviceAccounts { get; set; } = new List<DeviceAccount>();
        public ICollection<LoginHistory> LoginHistories { get; set; } = new List<LoginHistory>();
    }
}