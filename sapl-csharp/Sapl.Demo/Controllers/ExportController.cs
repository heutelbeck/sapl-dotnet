using Microsoft.AspNetCore.Mvc;
using Sapl.Core.Authorization;
using Sapl.Core.Constraints;
using Sapl.Core.Enforcement;

namespace Sapl.Demo.Controllers;

[ApiController]
[Route("api")]
public sealed class ExportController : ControllerBase
{
    private readonly EnforcementEngine _engine;
    private readonly ILogger<ExportController> _logger;

    public ExportController(EnforcementEngine engine, ILogger<ExportController> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    [HttpGet("exportData/{pilotId}/{sequenceId}")]
    public async Task<IActionResult> GetExportData(string pilotId, string sequenceId)
    {
        var token = ExtractBearerToken();
        if (token is null)
        {
            return Unauthorized(new { error = "Bearer token required" });
        }

        var sub = AuthorizationSubscription.Create(
            "anonymous",
            "exportData",
            new { pilotId, sequenceId },
            secrets: new { jwt = token });

        var result = await _engine.PreEnforceAsync(sub, HttpContext.RequestAborted);
        if (!result.IsPermitted)
        {
            return StatusCode(403, new { error = "Access denied by policy" });
        }

        _logger.LogInformation("exportData pilot={PilotId} sequence={SequenceId}", pilotId, sequenceId);
        return Ok(new { pilotId, sequenceId, data = "export-payload" });
    }

    [HttpGet("exportData2/{pilotId}/{sequenceId}")]
    public async Task<IActionResult> GetExportData2(string pilotId, string sequenceId)
    {
        var token = ExtractBearerToken();
        if (token is null)
        {
            return Unauthorized(new { error = "Bearer token required" });
        }

        var sub = AuthorizationSubscription.Create(
            "anonymous",
            "exportData",
            new { pilotId, sequenceId },
            secrets: new { jwt = token });

        var result = await _engine.PreEnforceAsync(sub, HttpContext.RequestAborted);
        if (!result.IsPermitted)
        {
            return Ok(new { error = "access_denied", decision = "DENY" });
        }

        _logger.LogInformation("exportData2 pilot={PilotId} sequence={SequenceId}", pilotId, sequenceId);
        return Ok(new { pilotId, sequenceId, data = "export-payload" });
    }

    private string? ExtractBearerToken()
    {
        var authHeader = HttpContext.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return authHeader["Bearer ".Length..];
    }
}
