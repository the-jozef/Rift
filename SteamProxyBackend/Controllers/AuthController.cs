using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SteamProxyBackend.Models;
using SteamProxyBackend.Data;
using SteamProxyBackend.DTOs;

namespace SteamProxyBackend.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;

        public AuthController(AppDbContext db) { _db = db; }

        // ─── REGISTER ─────────────────────────────────────────────────────────

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Username) ||
                    string.IsNullOrWhiteSpace(request.Password) ||
                    string.IsNullOrWhiteSpace(request.SteamId64))
                    return BadRequest(new AuthResponse { Success = false, Message = "All fields are required." });

                if (request.Password.Length < 6)
                    return BadRequest(new AuthResponse { Success = false, Message = "Password must be at least 6 characters." });

                bool alreadyExists = await _db.Users.AnyAsync(u =>
                    u.Username == request.Username || u.SteamId64 == request.SteamId64);

                if (alreadyExists)
                    return Conflict(new AuthResponse { Success = false, Message = "Username or Steam account already registered." });

                string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, 12);

                var user = new User
                {
                    Username = request.Username,
                    PasswordHash = passwordHash,
                    SteamId64 = request.SteamId64
                };

                _db.Users.Add(user);
                await _db.SaveChangesAsync();

                await LinkAccountToDevice(request.DeviceToken, user.Id, "Store");
                await SaveLoginHistory(user.Id, request.DeviceToken, "Steam");

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Account created successfully.",
                    UserId = user.Id,
                    Username = user.Username,
                    SteamId64 = user.SteamId64
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new AuthResponse { Success = false, Message = $"Server error: {ex.Message}" });
            }
        }

        // ─── LOGIN RIFT ────────────────────────────────────────────────────────

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                    return BadRequest(new AuthResponse { Success = false, Message = "Username and password are required." });

                var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);

                if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                    return Unauthorized(new AuthResponse { Success = false, Message = "Invalid username or password." });

                await LinkAccountToDevice(request.DeviceToken, user.Id, "Store");
                await SaveLoginHistory(user.Id, request.DeviceToken, "Rift");

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Login successful.",
                    UserId = user.Id,
                    Username = user.Username,
                    SteamId64 = user.SteamId64
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new AuthResponse { Success = false, Message = $"Server error: {ex.Message}" });
            }
        }

        // ─── LOGIN STEAM ───────────────────────────────────────────────────────

        [HttpPost("steam-login")]
        public async Task<IActionResult> SteamLogin([FromBody] SteamLoginRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.SteamId64))
                    return BadRequest(new AuthResponse { Success = false, Message = "SteamId64 is required." });

                var user = await _db.Users.FirstOrDefaultAsync(u => u.SteamId64 == request.SteamId64);

                if (user == null)
                    return NotFound(new AuthResponse { Success = false, Message = "No Rift account found for this Steam ID. Please register." });

                await LinkAccountToDevice(request.DeviceToken, user.Id, "Store");
                await SaveLoginHistory(user.Id, request.DeviceToken, "Steam");

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Steam login successful.",
                    UserId = user.Id,
                    Username = user.Username,
                    SteamId64 = user.SteamId64
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new AuthResponse { Success = false, Message = $"Server error: {ex.Message}" });
            }
        }

        // ─── HELPERS ──────────────────────────────────────────────────────────

        private async Task LinkAccountToDevice(string deviceToken, Guid userId, string lastLocation)
        {
            if (string.IsNullOrWhiteSpace(deviceToken)) return;

            var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceToken == deviceToken);
            if (device == null)
            {
                _db.Devices.Add(new Device { DeviceToken = deviceToken });
                await _db.SaveChangesAsync();
            }

            var otherAccounts = await _db.DeviceAccounts
                .Where(da => da.DeviceToken == deviceToken && da.UserId != userId)
                .ToListAsync();

            foreach (var acc in otherAccounts)
                acc.IsLastActive = false;

            var existing = await _db.DeviceAccounts
                .FirstOrDefaultAsync(da => da.DeviceToken == deviceToken && da.UserId == userId);

            if (existing != null)
            {
                existing.LastLoginAt = DateTime.UtcNow;
                existing.LastLocation = lastLocation;
                existing.IsLastActive = true;
            }
            else
            {
                _db.DeviceAccounts.Add(new DeviceAccount
                {
                    DeviceToken = deviceToken,
                    UserId = userId,
                    LastLocation = lastLocation,
                    IsLastActive = true
                });
            }

            await _db.SaveChangesAsync();
        }

        private async Task SaveLoginHistory(Guid userId, string deviceToken, string method)
        {
            if (string.IsNullOrWhiteSpace(deviceToken)) return;

            _db.LoginHistories.Add(new LoginHistory
            {
                UserId = userId,
                DeviceToken = deviceToken,
                LoginMethod = method,
                LoginAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
        }
    }
}