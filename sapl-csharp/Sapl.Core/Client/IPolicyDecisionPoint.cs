using Sapl.Core.Authorization;

namespace Sapl.Core.Client;

public interface IPolicyDecisionPoint
{
    Task<AuthorizationDecision> DecideOnceAsync(
        AuthorizationSubscription subscription,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AuthorizationDecision> Decide(
        AuthorizationSubscription subscription,
        CancellationToken cancellationToken = default);

    Task<MultiAuthorizationDecision> MultiDecideOnceAsync(
        MultiAuthorizationSubscription subscription,
        CancellationToken cancellationToken = default);

    Task<MultiAuthorizationDecision> MultiDecideAllOnceAsync(
        MultiAuthorizationSubscription subscription,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<IdentifiableAuthorizationDecision> MultiDecide(
        MultiAuthorizationSubscription subscription,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<MultiAuthorizationDecision> MultiDecideAll(
        MultiAuthorizationSubscription subscription,
        CancellationToken cancellationToken = default);
}
