using Microsoft.AspNetCore.Mvc;
using Sapl.Core.Authorization;
using Sapl.Core.Client;

namespace Sapl.Demo.Controllers;

[ApiController]
[Route("api")]
public sealed class HelloController : ControllerBase
{
    private readonly IPolicyDecisionPoint _pdp;
    private readonly ILogger<HelloController> _logger;

    public HelloController(IPolicyDecisionPoint pdp, ILogger<HelloController> logger)
    {
        _pdp = pdp;
        _logger = logger;
    }

    [HttpGet("hello")]
    public async Task<IActionResult> GetHello()
    {
        var decision = await _pdp.DecideOnceAsync(
            AuthorizationSubscription.Create("anonymous", "read", "hello"),
            HttpContext.RequestAborted);

        _logger.LogInformation("PDP decision: {Decision}", decision.Decision);

        if (decision.Decision == Decision.Permit
            && (decision.Obligations is null || decision.Obligations.Count == 0)
            && !decision.Resource.HasValue)
        {
            return Ok(new { message = "hello" });
        }

        return StatusCode(403, new { error = "Access denied by policy" });
    }
}
