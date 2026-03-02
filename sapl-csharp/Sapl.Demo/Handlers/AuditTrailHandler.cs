using System.Collections.Concurrent;
using System.Text.Json;
using Sapl.Core.Constraints.Api;

namespace Sapl.Demo.Handlers;

public sealed class AuditTrailHandler : IConsumerConstraintHandlerProvider
{
    private readonly ILogger<AuditTrailHandler> _logger;
    private readonly ConcurrentBag<object> _auditLog = [];

    public AuditTrailHandler(ILogger<AuditTrailHandler> logger)
    {
        _logger = logger;
    }

    public bool IsResponsible(JsonElement constraint)
    {
        return constraint.TryGetProperty("type", out var t) && t.GetString() == "auditTrail";
    }

    public Action<object> GetHandler(JsonElement constraint)
    {
        var action = constraint.TryGetProperty("action", out var a) ? a.GetString() ?? "unknown" : "unknown";
        return value =>
        {
            var entry = new { timestamp = DateTime.UtcNow.ToString("o"), action, value };
            _auditLog.Add(entry);
            _logger.LogInformation("[AUDIT] {Action}: recorded response", action);
        };
    }

    public IReadOnlyList<object> GetAuditLog() => [.. _auditLog];
}
