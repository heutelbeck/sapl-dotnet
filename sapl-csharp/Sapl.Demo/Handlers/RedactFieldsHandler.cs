using System.Text.Json;
using System.Text.Json.Nodes;
using Sapl.Core.Authorization;
using Sapl.Core.Constraints.Api;

namespace Sapl.Demo.Handlers;

public sealed class RedactFieldsHandler : IMappingConstraintHandlerProvider
{
    private readonly ILogger<RedactFieldsHandler> _logger;

    public RedactFieldsHandler(ILogger<RedactFieldsHandler> logger)
    {
        _logger = logger;
    }

    public bool IsResponsible(JsonElement constraint)
    {
        return constraint.TryGetProperty("type", out var t) && t.GetString() == "redactFields";
    }

    public int Priority => 0;

    public Func<object, object> GetHandler(JsonElement constraint)
    {
        var fields = new List<string>();
        if (constraint.TryGetProperty("fields", out var f) && f.ValueKind == JsonValueKind.Array)
        {
            foreach (var field in f.EnumerateArray())
            {
                var name = field.GetString();
                if (name is not null)
                {
                    fields.Add(name);
                }
            }
        }

        return value =>
        {
            var json = value is JsonElement el
                ? el.GetRawText()
                : JsonSerializer.Serialize(value, SerializerDefaults.Options);
            var node = JsonNode.Parse(json);
            if (node is JsonObject obj)
            {
                foreach (var field in fields)
                {
                    if (obj.ContainsKey(field))
                    {
                        obj[field] = "[REDACTED]";
                        _logger.LogInformation("[REDACT] Redacting field: {Field}", field);
                    }
                }
                var resultJson = obj.ToJsonString();
                return JsonDocument.Parse(resultJson).RootElement.Clone();
            }
            return value;
        };
    }
}
