using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Sapl.Core.Constraints;

namespace Sapl.AspNetCore.Middleware;

public sealed class AccessDeniedMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AccessDeniedMiddleware> _logger;

    public AccessDeniedMiddleware(RequestDelegate next, ILogger<AccessDeniedMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (AccessDeniedException ex)
        {
            _logger.LogDebug(ex, "Access denied.");
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("""{"error":"Access denied"}""")
                    .ConfigureAwait(false);
            }
        }
    }
}
