using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SteamProxyBackend.Data;
using SteamProxyBackend.DTOs;
using SteamProxyBackend.Models;

namespace SteamProxyBackend.Controllers
{
    [ApiController]
    [Route("api/device")]
    public class DeviceController : ControllerBase
    {
        private readonly AppDbContext _db;

        public DeviceController(AppDbContext db) { _db = db; }

        // ─── INIT DEVICE ──────────────────────────────────────────────────────

        [HttpPost("init")]
        public async Task<IActionResult> InitDevice([FromBody] DeviceInitRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.DeviceToken))
                    return BadRequest(new { Success = false, Message = "Device token is required." });

                var device = await _db.Devices
                    .FirstOrDefaultAsync(d => d.DeviceToken == request.DeviceToken);

                if (device == null)
                {
                    _db.Devices.Add(new Device { DeviceToken = request.DeviceToken });
                    await _db.SaveChangesAsync();
                    return Ok(new { Success = true, Message = "Device registered." });
                }

                return Ok(new { Success = true, Message = "Device already known." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        // ─── GET ALL ACCOUNTS ON DEVICE ───────────────────────────────────────

        [HttpGet("{token}/accounts")]
        public async Task<IActionResult> GetAccounts(string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                    return BadRequest(new { Success = false, Message = "Device token is required." });

                var accounts = await _db.DeviceAccounts
                    .Where(da => da.DeviceToken == token)
                    .Include(da => da.User)
                    .OrderByDescending(da => da.LastLoginAt)
                    .Select(da => new AccountInfo
                    {
                        UserId = da.UserId,
                        Username = da.User!.Username,
                        SteamId64 = da.User!.SteamId64,
                        LastLocation = da.LastLocation,
                        LastLoginAt = da.LastLoginAt,
                        IsLastActive = da.IsLastActive
                    })
                    .ToListAsync();

                return Ok(accounts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        // ─── GET LAST SESSION ─────────────────────────────────────────────────
        // FIXED: No longer joins Devices table — only uses DeviceAccounts + Users
        [HttpGet("{token}/session")]
        public async Task<IActionResult> GetSession(string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                    return BadRequest(new { Success = false, Message = "Device token is required." });

                var lastActive = await _db.DeviceAccounts
                    .Where(da => da.DeviceToken == token && da.IsLastActive)
                    .Include(da => da.User)
                    .FirstOrDefaultAsync();

                if (lastActive == null || lastActive.User == null)
                    return Ok(new SessionResponse { HasSession = false });

                return Ok(new SessionResponse
                {
                    HasSession = true,
                    UserId = lastActive.UserId,
                    Username = lastActive.User.Username,
                    SteamId64 = lastActive.User.SteamId64,
                    LastLocation = lastActive.LastLocation
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        // ─── SAVE SESSION ─────────────────────────────────────────────────────

        [HttpPost("session")]
        public async Task<IActionResult> SaveSession([FromBody] SessionRequest request)
        {
            try
            {
                var deviceAccount = await _db.DeviceAccounts
                    .FirstOrDefaultAsync(da =>
                        da.DeviceToken == request.DeviceToken &&
                        da.UserId == request.UserId);

                if (deviceAccount == null)
                    return NotFound(new { Success = false, Message = "Account not found on this device." });

                deviceAccount.LastLocation = request.LastLocation;
                deviceAccount.LastLoginAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        // ─── REMOVE ACCOUNT FROM DEVICE ───────────────────────────────────────

        [HttpDelete("{token}/account/{userId}")]
        public async Task<IActionResult> RemoveAccountFromDevice(string token, Guid userId)
        {
            try
            {
                var deviceAccount = await _db.DeviceAccounts
                    .FirstOrDefaultAsync(da => da.DeviceToken == token && da.UserId == userId);

                if (deviceAccount == null)
                    return NotFound(new { Success = false, Message = "Account not found on this device." });

                _db.DeviceAccounts.Remove(deviceAccount);
                await _db.SaveChangesAsync();

                return Ok(new { Success = true, Message = "Account removed from this device." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        // ─── DELETE ACCOUNT FULLY ─────────────────────────────────────────────

        [HttpDelete("account/{userId}/full")]
        public async Task<IActionResult> DeleteAccountFully(Guid userId)
        {
            try
            {
                var user = await _db.Users.FindAsync(userId);

                if (user == null)
                    return NotFound(new { Success = false, Message = "Account not found." });

                var deviceAccounts = _db.DeviceAccounts.Where(da => da.UserId == userId);
                var loginHistories = _db.LoginHistories.Where(lh => lh.UserId == userId);

                _db.DeviceAccounts.RemoveRange(deviceAccounts);
                _db.LoginHistories.RemoveRange(loginHistories);
                _db.Users.Remove(user);

                await _db.SaveChangesAsync();

                return Ok(new { Success = true, Message = "Account permanently deleted." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }
    }
}