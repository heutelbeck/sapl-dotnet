using System.Text.Json;
using Sapl.Core.Constraints.Api;

namespace Sapl.Demo.Handlers;

public sealed class InjectTimestampHandler : IMethodInvocationConstraintHandlerProvider
{
    private readonly ILogger<InjectTimestampHandler> _logger;

    public InjectTimestampHandler(ILogger<InjectTimestampHandler> logger)
    {
        _logger = logger;
    }

    public bool IsResponsible(JsonElement constraint)
    {
        return constraint.TryGetProperty("type", out var t) && t.GetString() == "injectTimestamp";
    }

    public Action<MethodInvocationContext> GetHandler(JsonElement constraint)
    {
        return context =>
        {
            var timestamp = DateTime.UtcNow.ToString("o");
            if (context.Request is Microsoft.AspNetCore.Http.HttpRequest httpRequest)
            {
                httpRequest.HttpContext.Items["policyTimestamp"] = timestamp;
            }
            _logger.LogInformation("[METHOD] Injected policy timestamp: {Timestamp}", timestamp);
        };
    }
}
