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
        // FIXED: Raw SQL — no EF navigation properties

        [HttpGet("{token}/accounts")]
        public async Task<IActionResult> GetAccounts(string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                    return BadRequest(new { Success = false, Message = "Device token is required." });

                var accounts = await _db.Database.SqlQueryRaw<AccountInfo>(@"
                    SELECT 
                        da.""UserId"",
                        u.""Username"",
                        u.""SteamId64"",
                        da.""LastLocation"",
                        da.""LastLoginAt"",
                        da.""IsLastActive""
                    FROM ""DeviceAccounts"" da
                    INNER JOIN ""Users"" u ON da.""UserId"" = u.""Id""
                    WHERE da.""DeviceToken"" = {0}
                    ORDER BY da.""LastLoginAt"" DESC", token)
                    .ToListAsync();

                return Ok(accounts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        // ─── GET LAST SESSION ─────────────────────────────────────────────────
        // FIXED: Raw SQL — no EF navigation properties

        [HttpGet("{token}/session")]
        public async Task<IActionResult> GetSession(string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                    return BadRequest(new { Success = false, Message = "Device token is required." });

                var results = await _db.Database.SqlQueryRaw<SessionResponse>(@"
                    SELECT 
                        TRUE as ""HasSession"",
                        da.""UserId"",
                        u.""Username"",
                        u.""SteamId64"",
                        da.""LastLocation""
                    FROM ""DeviceAccounts"" da
                    INNER JOIN ""Users"" u ON da.""UserId"" = u.""Id""
                    WHERE da.""DeviceToken"" = {0} AND da.""IsLastActive"" = TRUE
                    LIMIT 1", token)
                    .ToListAsync();

                if (results == null || results.Count == 0)
                    return Ok(new SessionResponse { HasSession = false });

                return Ok(results[0]);
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
                var rows = await _db.Database.ExecuteSqlRawAsync(@"
                    UPDATE ""DeviceAccounts""
                    SET ""LastLocation"" = {0}, ""LastLoginAt"" = NOW()
                    WHERE ""DeviceToken"" = {1} AND ""UserId"" = {2}",
                    request.LastLocation, request.DeviceToken, request.UserId);

                if (rows == 0)
                    return NotFound(new { Success = false, Message = "Account not found on this device." });

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
                var rows = await _db.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM ""DeviceAccounts""
                    WHERE ""DeviceToken"" = {0} AND ""UserId"" = {1}",
                    token, userId);

                if (rows == 0)
                    return NotFound(new { Success = false, Message = "Account not found on this device." });

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
                await _db.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM ""LoginHistories"" WHERE ""UserId"" = {0}", userId);

                await _db.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM ""DeviceAccounts"" WHERE ""UserId"" = {0}", userId);

                var rows = await _db.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM ""Users"" WHERE ""Id"" = {0}", userId);

                if (rows == 0)
                    return NotFound(new { Success = false, Message = "Account not found." });

                return Ok(new { Success = true, Message = "Account permanently deleted." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }
    }
}