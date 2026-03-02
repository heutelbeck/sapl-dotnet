using System.Text.Json;
using Sapl.Core.Constraints.Api;

namespace Sapl.Demo.Handlers;

public sealed class LogAccessHandler : IRunnableConstraintHandlerProvider
{
    private readonly ILogger<LogAccessHandler> _logger;

    public LogAccessHandler(ILogger<LogAccessHandler> logger)
    {
        _logger = logger;
    }

    public bool IsResponsible(JsonElement constraint)
    {
        return constraint.TryGetProperty("type", out var t) && t.GetString() == "logAccess";
    }

    public Signal Signal => Signal.OnDecision;

    public Action GetHandler(JsonElement constraint)
    {
        var message = constraint.TryGetProperty("message", out var m) ? m.GetString() ?? "Access logged" : "Access logged";
        return () => _logger.LogInformation("[POLICY] {Message}", message);
    }
}
