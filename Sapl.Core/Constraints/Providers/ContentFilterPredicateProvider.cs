using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sapl.Core.Constraints.Api;

namespace Sapl.Core.Constraints.Providers;

public sealed class ContentFilterPredicateProvider : IFilterPredicateConstraintHandlerProvider
{
    private readonly ILogger<ContentFilterPredicateProvider> _logger;

    public ContentFilterPredicateProvider(ILogger<ContentFilterPredicateProvider> logger)
    {
        _logger = logger;
    }

    public bool IsResponsible(JsonElement constraint) =>
        constraint.ValueKind == JsonValueKind.Object &&
        constraint.TryGetProperty("type", out var typeProp) &&
        typeProp.GetString() == "filterByClassification";

    public Func<object, bool> GetHandler(JsonElement constraint)
    {
        var maxLevel = 1;
        if (constraint.TryGetProperty("maxLevel", out var levelProp) &&
            levelProp.ValueKind == JsonValueKind.Number)
        {
            maxLevel = levelProp.GetInt32();
        }

        var levelMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["PUBLIC"] = 0,
            ["INTERNAL"] = 1,
            ["CONFIDENTIAL"] = 2,
            ["SECRET"] = 3,
        };

        return value =>
        {
            if (value is JsonElement element &&
                element.TryGetProperty("classification", out var classProp) &&
                classProp.ValueKind == JsonValueKind.String)
            {
                var classification = classProp.GetString()!;
                if (levelMap.TryGetValue(classification, out var level))
                {
                    return level <= maxLevel;
                }
                _logger.LogDebug("Unknown classification: {Classification}", classification);
                return false;
            }

            if (value is not null)
            {
                var json = JsonSerializer.Serialize(value);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("classification", out var cp) &&
                    cp.ValueKind == JsonValueKind.String)
                {
                    var classification = cp.GetString()!;
                    if (levelMap.TryGetValue(classification, out var level))
                    {
                        return level <= maxLevel;
                    }
                }
            }

            return true;
        };
    }
}
