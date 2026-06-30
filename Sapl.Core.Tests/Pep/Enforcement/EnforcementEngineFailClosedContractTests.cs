using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentAssertions;
using Sapl.Core.Authorization;
using Sapl.Core.Client;
using Sapl.Core.Constraints;
using Sapl.Core.Pep.Constraints;
using Sapl.Core.Pep.Enforcement;
using Xunit;

namespace Sapl.Core.Tests.Pep.Enforcement;

/// <summary>
/// Fail-closed contract scenarios that the streaming and pre-invocation pipelines must honour
/// to match the Spring PEP. Two operational concerns are covered: what happens when the PDP
/// decision source stops emitting (it is contractually infinite), and whether the pre-invocation
/// input signal still fires when a decision-scoped obligation has already failed.
/// </summary>
public sealed class EnforcementEngineFailClosedContractTests
{
    private static readonly AuthorizationSubscription Subscription =
        AuthorizationSubscription.Create("alice", "read", "doc");

    private static JsonElement Constraint(object value) => JsonSerializer.SerializeToElement(value);

    private static AuthorizationDecision Permit(params object[] obligations) => new()
    {
        Decision = Decision.Permit,
        Obligations = obligations.Length == 0 ? null : obligations.Select(Constraint).ToArray(),
    };

    private static async IAsyncEnumerable<int> NeverEndingItems(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            yield return 0;
        }
    }

    private static async IAsyncEnumerable<int> FiniteItems(params int[] values)
    {
        foreach (var value in values)
        {
            await Task.Yield();
            yield return value;
        }
    }

    /// <summary>
    /// FSM-PDP-COMPLETE-IS-ERROR / AP-PDP-COMPLETE-AS-NORMAL. The decision source is contractually
    /// infinite; the PEP itself must turn a stop in that source into a fail-closed termination rather
    /// than leaving the protected stream running without a live authorization. STREAM-FSM-02.
    /// </summary>
    public sealed class PdpDecisionStreamCompletion
    {
        [Fact]
        async Task WhenDecisionStreamCompletesWhilePermittingThenProtectedStreamFailsClosed()
        {
            // Spring: onPdpComplete reports IllegalStateException; the protected stream terminates
            // with that error instead of flowing items with no live authorization (fail-open).
            var engine = new EnforcementEngine(new CompletingStreamPdp(Permit()), []);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            var act = async () =>
            {
                await foreach (var _ in engine.EnforceStreamAsync(
                                   Subscription, NeverEndingItems(cts.Token), cancellationToken: cts.Token)
                                   .ConfigureAwait(false))
                {
                }
            };

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        async Task WhenDecisionStreamIsEmptyThenProtectedStreamIsDenied()
        {
            // Spring: switchIfEmpty coerces an empty decision flux into a single DENY, so an empty
            // decision source denies access rather than completing normally with no items.
            var engine = new EnforcementEngine(new CompletingStreamPdp(), []);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            var act = async () =>
            {
                await foreach (var _ in engine.EnforceStreamAsync(
                                   Subscription, FiniteItems(1, 2, 3), cancellationToken: cts.Token)
                                   .ConfigureAwait(false))
                {
                }
            };

            await act.Should().ThrowAsync<AccessDeniedException>();
        }
    }

    /// <summary>
    /// R16 / A11. Pre-invocation enforcement fires the decision signal and then the input signal
    /// unconditionally, and only afterwards denies. A failed decision-scoped obligation must not
    /// suppress the input-scoped handlers. F6.
    /// </summary>
    public sealed class PreInvocationSignalDischarge
    {
        [Fact]
        async Task WhenDecisionObligationFailsThenInputSignalStillFiresBeforeDeny()
        {
            var decisionRan = false;
            var inputRan = false;
            var decision = Permit(new { type = "decision-audit" }, new { type = "input-audit" });
            var engine = new EnforcementEngine(
                new OneShotPdp(decision),
                [
                    new ScopedRunnerProvider("decision-audit", SignalType.Decision, () =>
                    {
                        decisionRan = true;
                        throw new InvalidOperationException("decision obligation handler failed");
                    }),
                    new ScopedRunnerProvider("input-audit", SignalType.Input, () => inputRan = true),
                ]);

            var act = () => engine.PreDecideAsync(Subscription, typeof(string));

            await act.Should().ThrowAsync<AccessDeniedException>();
            decisionRan.Should().BeTrue("the decision signal fires during pre-invocation enforcement");
            inputRan.Should().BeTrue("the input signal fires unconditionally before the pre-invocation deny");
        }
    }

    private sealed class CompletingStreamPdp(params AuthorizationDecision[] decisions) : IPolicyDecisionPoint
    {
        public Task<AuthorizationDecision> DecideOnceAsync(AuthorizationSubscription s, CancellationToken c = default) =>
            Task.FromResult(decisions.Length > 0 ? decisions[0] : new AuthorizationDecision { Decision = Decision.Deny });

        public async IAsyncEnumerable<AuthorizationDecision> Decide(
            AuthorizationSubscription s,
            [EnumeratorCancellation] CancellationToken c = default)
        {
            foreach (var decision in decisions)
            {
                await Task.Yield();
                yield return decision;
            }
            // The decision source stops here: a finite, legal IAsyncEnumerable that the PEP must
            // treat as a defect (non-empty) or a DENY (empty), never as a benign completion.
        }

        public Task<MultiAuthorizationDecision> MultiDecideAllOnceAsync(MultiAuthorizationSubscription s, CancellationToken c = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<IdentifiableAuthorizationDecision> MultiDecide(MultiAuthorizationSubscription s, CancellationToken c = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<MultiAuthorizationDecision> MultiDecideAll(MultiAuthorizationSubscription s, CancellationToken c = default) =>
            throw new NotSupportedException();
    }

    private sealed class OneShotPdp(AuthorizationDecision decision) : IPolicyDecisionPoint
    {
        public Task<AuthorizationDecision> DecideOnceAsync(AuthorizationSubscription s, CancellationToken c = default) =>
            Task.FromResult(decision);

        public async IAsyncEnumerable<AuthorizationDecision> Decide(
            AuthorizationSubscription s,
            [EnumeratorCancellation] CancellationToken c = default)
        {
            await Task.Yield();
            yield return decision;
        }

        public Task<MultiAuthorizationDecision> MultiDecideAllOnceAsync(MultiAuthorizationSubscription s, CancellationToken c = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<IdentifiableAuthorizationDecision> MultiDecide(MultiAuthorizationSubscription s, CancellationToken c = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<MultiAuthorizationDecision> MultiDecideAll(MultiAuthorizationSubscription s, CancellationToken c = default) =>
            throw new NotSupportedException();
    }

    private sealed class ScopedRunnerProvider(string type, SignalType signal, Action onRun) : IConstraintHandlerProvider
    {
        public IReadOnlyList<ScopedHandler> GetConstraintHandlers(JsonElement constraint, IReadOnlySet<SignalType> supportedSignals) =>
            IConstraintHandlerProvider.ConstraintIsOfType(constraint, type)
                ? [new ScopedHandler(new ConstraintHandler.Runner(onRun), signal, 0)]
                : [];
    }
}
