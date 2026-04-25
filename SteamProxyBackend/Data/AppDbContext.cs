using Microsoft.EntityFrameworkCore;
using SteamProxyBackend.Models;
using System.Reflection.Emit;

namespace SteamProxyBackend.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Device> Devices { get; set; }
        public DbSet<DeviceAccount> DeviceAccounts { get; set; }
        public DbSet<LoginHistory> LoginHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ─── USER ─────────────────────────────────────────────────────
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.Username).HasColumnName("Username").IsRequired();
                entity.Property(e => e.PasswordHash).HasColumnName("PasswordHash").IsRequired();
                entity.Property(e => e.SteamId64).HasColumnName("SteamId64").IsRequired();
                entity.Property(e => e.Email).HasColumnName("Email");
                entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.SteamId64).IsUnique();
            });

            // ─── DEVICE ───────────────────────────────────────────────────
            modelBuilder.Entity<Device>(entity =>
            {
                entity.ToTable("Devices");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.DeviceToken).HasColumnName("DeviceToken").IsRequired();
                entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
                entity.HasIndex(e => e.DeviceToken).IsUnique();
            });

            // ─── DEVICE ACCOUNT ───────────────────────────────────────────
            modelBuilder.Entity<DeviceAccount>(entity =>
            {
                entity.ToTable("DeviceAccounts");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.DeviceToken).HasColumnName("DeviceToken").IsRequired();
                entity.Property(e => e.UserId).HasColumnName("UserId").IsRequired();
                entity.Property(e => e.LastLocation).HasColumnName("LastLocation");
                entity.Property(e => e.LastLoginAt).HasColumnName("LastLoginAt");
                entity.Property(e => e.IsLastActive).HasColumnName("IsLastActive");
                entity.HasIndex(e => new { e.DeviceToken, e.UserId }).IsUnique();

                entity.HasOne(e => e.User)
                      .WithMany(u => u.DeviceAccounts)
                      .HasForeignKey(e => e.UserId);
            });

            // ─── LOGIN HISTORY ────────────────────────────────────────────
            modelBuilder.Entity<LoginHistory>(entity =>
            {
                entity.ToTable("LoginHistories");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.UserId).HasColumnName("UserId").IsRequired();
                entity.Property(e => e.DeviceToken).HasColumnName("DeviceToken").IsRequired();
                entity.Property(e => e.LoginAt).HasColumnName("LoginAt");
                entity.Property(e => e.LoginMethod).HasColumnName("LoginMethod").IsRequired();

                entity.HasOne(e => e.User)
                      .WithMany(u => u.LoginHistories)
                      .HasForeignKey(e => e.UserId);
            });
        }
    }
}