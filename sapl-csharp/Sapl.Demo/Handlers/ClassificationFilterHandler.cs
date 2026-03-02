using System.Text.Json;
using Sapl.Core.Authorization;
using Sapl.Core.Constraints.Api;

namespace Sapl.Demo.Handlers;

public sealed class ClassificationFilterHandler : IFilterPredicateConstraintHandlerProvider
{
    private static readonly Dictionary<string, int> ClassificationLevels = new()
    {
        ["PUBLIC"] = 0,
        ["INTERNAL"] = 1,
        ["CONFIDENTIAL"] = 2,
        ["SECRET"] = 3,
    };

    private readonly ILogger<ClassificationFilterHandler> _logger;

    public ClassificationFilterHandler(ILogger<ClassificationFilterHandler> logger)
    {
        _logger = logger;
    }

    public bool IsResponsible(JsonElement constraint)
    {
        return constraint.TryGetProperty("type", out var t) && t.GetString() == "filterByClassification";
    }

    public Func<object, bool> GetHandler(JsonElement constraint)
    {
        var maxLevel = constraint.TryGetProperty("maxLevel", out var m) ? m.GetString() ?? "PUBLIC" : "PUBLIC";
        var maxRank = ClassificationLevels.GetValueOrDefault(maxLevel, 0);

        return element =>
        {
            string? classification = null;
            if (element is JsonElement je && je.TryGetProperty("classification", out var c))
            {
                classification = c.GetString();
            }
            else
            {
                var json = JsonSerializer.Serialize(element, SerializerDefaults.Options);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("classification", out var cls))
                {
                    classification = cls.GetString();
                }
            }

            if (classification is null || !ClassificationLevels.TryGetValue(classification, out var rank))
            {
                _logger.LogWarning("[FILTER] Element excluded: unknown classification");
                return false;
            }

            if (rank > maxRank)
            {
                _logger.LogInformation("[FILTER] Excluded {Classification} element (max: {MaxLevel})", classification, maxLevel);
                return false;
            }

            return true;
        };
    }
}
