using System.Text.Json.Serialization;

namespace Sapl.Core.Authorization;

public sealed record IdentifiableAuthorizationDecision
{
    [JsonPropertyName("subscriptionId")]
    public required string SubscriptionId { get; init; }

    [JsonPropertyName("decision")]
    public required AuthorizationDecision Decision { get; init; }
}
