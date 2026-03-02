using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sapl.Core.Authorization;

public sealed record AuthorizationSubscription
{
    [JsonPropertyName("subject")]
    public required JsonElement Subject { get; init; }

    [JsonPropertyName("action")]
    public required JsonElement Action { get; init; }

    [JsonPropertyName("resource")]
    public required JsonElement Resource { get; init; }

    [JsonPropertyName("environment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Environment { get; init; }

    [JsonPropertyName("secrets")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Secrets { get; init; }

    public string ToJsonString(JsonSerializerOptions? options = null) =>
        JsonSerializer.Serialize(this, options ?? SerializerDefaults.Options);

    public string ToLoggableString(JsonSerializerOptions? options = null)
    {
        var loggable = new LoggableSubscription
        {
            Subject = Subject,
            Action = Action,
            Resource = Resource,
            Environment = Environment,
        };
        return JsonSerializer.Serialize(loggable, options ?? SerializerDefaults.Options);
    }

    public static AuthorizationSubscription Create(
        object? subject,
        object? action,
        object? resource,
        object? environment = null,
        object? secrets = null)
    {
        var options = SerializerDefaults.Options;
        return new AuthorizationSubscription
        {
            Subject = ToJsonElement(subject, options),
            Action = ToJsonElement(action, options),
            Resource = ToJsonElement(resource, options),
            Environment = environment is null ? null : ToJsonElement(environment, options),
            Secrets = secrets is null ? null : ToJsonElement(secrets, options),
        };
    }

    private static JsonElement ToJsonElement(object? value, JsonSerializerOptions options)
    {
        if (value is JsonElement element)
            return element;
        var json = JsonSerializer.Serialize(value, options);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private sealed record LoggableSubscription
    {
        [JsonPropertyName("subject")]
        public required JsonElement Subject { get; init; }

        [JsonPropertyName("action")]
        public required JsonElement Action { get; init; }

        [JsonPropertyName("resource")]
        public required JsonElement Resource { get; init; }

        [JsonPropertyName("environment")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? Environment { get; init; }
    }
}
