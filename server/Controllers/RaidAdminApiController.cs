using Microsoft.AspNetCore.Mvc;
using SPTarkov.Server.Core.Models.Common;
using RaidAdminPanel.Models;
using RaidAdminPanel.Services;

namespace RaidAdminPanel.Controllers;

[ApiController]
[Route("raidadminpanel/api")]
public class RaidAdminApiController(
    RaidAdminConfigService configService,
    RaidAdminService adminService,
    FikaBridgeService fikaBridge,
    RaidAdminActionLog actionLog) : ControllerBase
{
    private bool Authorize()
    {
        if (!configService.Config.RequireApiKey)
        {
            return true;
        }

        if (Request.Headers.TryGetValue("X-RaidAdmin-Key", out var key))
        {
            return configService.ValidateApiKey(key.ToString());
        }

        if (Request.Query.TryGetValue("key", out var qKey))
        {
            return configService.ValidateApiKey(qKey.ToString());
        }

        return false;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        if (!Authorize())
        {
            return Unauthorized(new { error = "Invalid API key. Set X-RaidAdmin-Key header or ?key=" });
        }

        return Ok(adminService.GetDashboardStatus());
    }

    [HttpGet("players")]
    public IActionResult GetPlayers([FromQuery] int activeMinutes = 60)
    {
        if (!Authorize())
        {
            return Unauthorized();
        }

        return Ok(adminService.GetPlayers(activeMinutes));
    }

    [HttpGet("raids")]
    public IActionResult GetRaids()
    {
        if (!Authorize())
        {
            return Unauthorized();
        }

        return Ok(fikaBridge.GetActiveRaids());
    }

    [HttpGet("logs")]
    public IActionResult GetLogs([FromQuery] int max = 50)
    {
        if (!Authorize())
        {
            return Unauthorized();
        }

        return Ok(actionLog.GetRecent(max));
    }

    [HttpPost("profiles/save-all")]
    public async Task<IActionResult> SaveAllProfiles()
    {
        if (!Authorize())
        {
            return Unauthorized();
        }

        var count = await adminService.SaveAllProfilesAsync();
        return Ok(new { saved = count });
    }

    [HttpPost("profiles/{profileId}/save")]
    public async Task<IActionResult> SaveProfile(string profileId)
    {
        if (!Authorize())
        {
            return Unauthorized();
        }

        if (!MongoId.IsValidMongoId(profileId))
        {
            return BadRequest(new { error = "Invalid profileId" });
        }

        try
        {
            await adminService.SaveProfileAsync(new MongoId(profileId));
            return Ok(new { profileId, saved = true });
        }
        catch (Exception ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("players/{profileId}/force-extract")]
    public IActionResult ForceExtractPlayer(string profileId, [FromBody] ForceExtractRequest? body)
    {
        if (!Authorize())
        {
            return Unauthorized();
        }

        if (!MongoId.IsValidMongoId(profileId))
        {
            return BadRequest(new { error = "Invalid profileId" });
        }

        var cmd = adminService.QueueForceExtract(
            new MongoId(profileId),
            body?.MatchId,
            body?.Reason ?? "Admin: принудительная высадка (survived)");

        return Ok(cmd);
    }

    [HttpPost("players/{profileId}/request-inventory-snapshot")]
    public IActionResult RequestInventorySnapshot(string profileId, [FromBody] ForceExtractRequest? body)
    {
        if (!Authorize())
        {
            return Unauthorized();
        }

        if (!MongoId.IsValidMongoId(profileId))
        {
            return BadRequest(new { error = "Invalid profileId" });
        }

        var cmd = adminService.QueueInventorySnapshot(
            new MongoId(profileId),
            body?.Reason ?? "Admin: снимок инвентаря в рейде");

        return Ok(cmd);
    }

    [HttpPost("raids/force-extract-all")]
    public IActionResult ForceExtractRaid([FromBody] ForceExtractRequest body)
    {
        if (!Authorize())
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(body.MatchId))
        {
            return BadRequest(new { error = "matchId required" });
        }

        try
        {
            var cmds = adminService.QueueForceExtractForMatch(
                body.MatchId,
                body.IncludeDead,
                body.Reason ?? "Admin: массовая высадка рейда");

            return Ok(new { queued = cmds.Count, commands = cmds });
        }
        catch (Exception ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("raids/{matchId}/end-session")]
    public IActionResult EndFikaSession(string matchId)
    {
        if (!Authorize())
        {
            return Unauthorized();
        }

        if (!MongoId.IsValidMongoId(matchId))
        {
            return BadRequest(new { error = "Invalid matchId" });
        }

        var ok = adminService.TryEndFikaMatch(new MongoId(matchId), out var message);
        return ok ? Ok(new { message }) : BadRequest(new { error = message });
    }

    /// <summary>
    /// Прямой force extract через Fika API (server-side, без polling).
    /// Завершает рейд для конкретного игрока.
    /// </summary>
    [HttpPost("players/{profileId}/force-extract-direct")]
    public IActionResult ForceExtractPlayerDirect(string profileId, [FromBody] ForceExtractRequest? body)
    {
        if (!Authorize())
        {
            return Unauthorized();
        }

        if (!MongoId.IsValidMongoId(profileId))
        {
            return BadRequest(new { error = "Invalid profileId" });
        }

        try
        {
            var matchId = body?.MatchId ?? adminService.GetMatchIdForPlayer(new MongoId(profileId));
            if (string.IsNullOrEmpty(matchId))
            {
                return BadRequest(new { error = "Player not in active Fika match" });
            }

            // Завершаем весь матч Fika для этого игрока
            var ok = adminService.TryEndFikaMatch(new MongoId(matchId), out var message);
            return ok ? Ok(new { message, profileId, matchId }) : BadRequest(new { error = message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
