using System.Text.Json.Serialization;

namespace Sapl.Core.Authorization;

public sealed record MultiAuthorizationDecision
{
    [JsonPropertyName("decisions")]
    public required IReadOnlyDictionary<string, AuthorizationDecision> Decisions { get; init; }

    public static MultiAuthorizationDecision IndeterminateForAll(
        MultiAuthorizationSubscription subscription)
    {
        var decisions = new Dictionary<string, AuthorizationDecision>();
        foreach (var id in subscription.Subscriptions.Keys)
        {
            decisions[id] = AuthorizationDecision.IndeterminateInstance;
        }
        return new MultiAuthorizationDecision { Decisions = decisions };
    }
}
