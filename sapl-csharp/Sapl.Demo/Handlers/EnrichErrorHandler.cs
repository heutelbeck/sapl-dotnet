using System.Text.Json;
using Sapl.Core.Constraints.Api;

namespace Sapl.Demo.Handlers;

public sealed class EnrichErrorHandler : IErrorMappingConstraintHandlerProvider
{
    private readonly ILogger<EnrichErrorHandler> _logger;

    public EnrichErrorHandler(ILogger<EnrichErrorHandler> logger)
    {
        _logger = logger;
    }

    public bool IsResponsible(JsonElement constraint)
    {
        return constraint.TryGetProperty("type", out var t) && t.GetString() == "enrichError";
    }

    public int Priority => 0;

    public Func<Exception, Exception> GetHandler(JsonElement constraint)
    {
        var supportUrl = constraint.TryGetProperty("supportUrl", out var s) ? s.GetString() ?? "https://support.example.com" : "https://support.example.com";

        return error =>
        {
            _logger.LogInformation("[ERROR-ENRICH] Enriching error with support URL: {SupportUrl}", supportUrl);
            return new InvalidOperationException($"{error.Message} | Support: {supportUrl}", error);
        };
    }
}
