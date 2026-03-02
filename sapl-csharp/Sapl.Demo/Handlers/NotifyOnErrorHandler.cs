using System.Text.Json;
using Sapl.Core.Constraints.Api;

namespace Sapl.Demo.Handlers;

public sealed class NotifyOnErrorHandler : IErrorHandlerProvider
{
    private readonly ILogger<NotifyOnErrorHandler> _logger;

    public NotifyOnErrorHandler(ILogger<NotifyOnErrorHandler> logger)
    {
        _logger = logger;
    }

    public bool IsResponsible(JsonElement constraint)
    {
        return constraint.TryGetProperty("type", out var t) && t.GetString() == "notifyOnError";
    }

    public Action<Exception> GetHandler(JsonElement constraint)
    {
        return error =>
        {
            _logger.LogWarning("[ERROR-NOTIFY] Error during policy-protected operation: {Message}", error.Message);
        };
    }
}
