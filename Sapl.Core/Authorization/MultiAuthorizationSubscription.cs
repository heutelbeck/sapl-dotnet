using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sapl.Core.Authorization;

public sealed record MultiAuthorizationSubscription
{
    [JsonPropertyName("subscriptions")]
    public required IReadOnlyDictionary<string, AuthorizationSubscription> Subscriptions { get; init; }

    public string ToJsonString(JsonSerializerOptions? options = null) =>
        JsonSerializer.Serialize(this, options ?? SerializerDefaults.Options);

    /// <summary>
    /// The node wire format for every multi endpoint: the bare id-to-subscription
    /// map with no envelope. The response is the matching bare id-to-decision map.
    /// </summary>
    public string ToWireJson(JsonSerializerOptions? options = null) =>
        JsonSerializer.Serialize(Subscriptions, options ?? SerializerDefaults.Options);

    public string ToLoggableString(JsonSerializerOptions? options = null)
    {
        var loggable = new Dictionary<string, string>();
        foreach (var (id, sub) in Subscriptions)
        {
            loggable[id] = sub.ToLoggableString(options);
        }
        return JsonSerializer.Serialize(
            new { subscriptions = loggable },
            options ?? SerializerDefaults.Options);
    }
}
