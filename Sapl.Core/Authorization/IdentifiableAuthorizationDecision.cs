using System.Text.Json.Serialization;

namespace Sapl.Core.Authorization;

public sealed record IdentifiableAuthorizationDecision
{
    [JsonPropertyName("authorizationSubscriptionId")]
    public required string SubscriptionId { get; init; }

    [JsonPropertyName("authorizationDecision")]
    public required AuthorizationDecision Decision { get; init; }
}
