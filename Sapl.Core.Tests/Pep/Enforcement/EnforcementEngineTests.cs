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

public sealed class EnforcementEngineTests
{
    private static readonly AuthorizationSubscription Subscription =
        AuthorizationSubscription.Create("alice", "read", "doc");

    private static JsonElement Constraint(object value) => JsonSerializer.SerializeToElement(value);

    private static AuthorizationDecision Permit(params object[] obligations) => new()
    {
        Decision = Decision.Permit,
        Obligations = obligations.Length == 0 ? null : obligations.Select(Constraint).ToArray(),
    };

    private static EnforcementEngine Engine(AuthorizationDecision decision, params IConstraintHandlerProvider[] providers) =>
        new(new StubPdp(decision), providers);

    [Fact]
    async Task PreEnforcePermitWithNoObligationsPasses()
    {
        var act = () => Engine(Permit()).PreEnforceAsync(Subscription);

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(Decision.Deny)]
    [InlineData(Decision.Indeterminate)]
    [InlineData(Decision.NotApplicable)]
    [InlineData(Decision.Suspend)]
    async Task PreEnforceNonPermitDenies(Decision verb)
    {
        var act = () => Engine(new AuthorizationDecision { Decision = verb }).PreEnforceAsync(Subscription);

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    async Task PreEnforceUnresolvedObligationDeniesFailClosed()
    {
        var act = () => Engine(Permit(new { type = "unknown" })).PreEnforceAsync(Subscription);

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    async Task PreEnforceRunsDecisionObligation()
    {
        var ran = false;
        var engine = Engine(Permit(new { type = "log" }), new RunnerProvider("log", () => ran = true));

        await engine.PreEnforceAsync(Subscription);

        ran.Should().BeTrue();
    }

    [Fact]
    async Task PreEnforceDenyStillRunsDecisionObligationThenDenies()
    {
        var ran = false;
        var decision = new AuthorizationDecision
        {
            Decision = Decision.Deny,
            Obligations = [Constraint(new { type = "audit" })],
        };
        var act = () => Engine(decision, new RunnerProvider("audit", () => ran = true)).PreEnforceAsync(Subscription);

        await act.Should().ThrowAsync<AccessDeniedException>();
        ran.Should().BeTrue();
    }

    [Fact]
    async Task PostEnforceTransformsOutputViaObligation()
    {
        var engine = Engine(Permit(new { type = "uppercase" }), new UppercaseProvider());

        var result = await engine.PostEnforceAsync(Subscription, "secret", typeof(string));

        result.Should().Be("SECRET");
    }

    [Fact]
    async Task PostEnforceSubstitutesDecisionResource()
    {
        var decision = new AuthorizationDecision { Decision = Decision.Permit, Resource = Constraint("substituted") };

        var result = await Engine(decision).PostEnforceAsync(Subscription, "original", typeof(string));

        result.Should().Be("substituted");
    }

    [Fact]
    async Task PostEnforceDenyThrows()
    {
        var act = () => Engine(new AuthorizationDecision { Decision = Decision.Deny })
            .PostEnforceAsync(Subscription, "x", typeof(string));

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    async Task StreamPermitFlowsItems()
    {
        var engine = Engine(Permit());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var items = new List<int>();
        await foreach (var item in engine.EnforceStreamAsync(Subscription, DelayedItems(1, 2, 3), cancellationToken: cts.Token))
        {
            items.Add(item);
        }

        items.Should().Equal(1, 2, 3);
    }

    [Fact]
    async Task StreamDenyTerminatesWithAccessDenied()
    {
        var engine = new EnforcementEngine(
            new StubPdp(new AuthorizationDecision { Decision = Decision.Deny }, kept: true),
            []);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var act = async () =>
        {
            await foreach (var _ in engine.EnforceStreamAsync(Subscription, NeverEndingItems(), cancellationToken: cts.Token))
            {
            }
        };

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    async Task StreamDenyStillRunsDecisionObligation()
    {
        var ran = false;
        var decision = new AuthorizationDecision
        {
            Decision = Decision.Deny,
            Obligations = [Constraint(new { type = "audit" })],
        };
        var engine = new EnforcementEngine(
            new StubPdp(decision, kept: true),
            [new RunnerProvider("audit", () => ran = true)]);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var act = async () =>
        {
            await foreach (var _ in engine.EnforceStreamAsync(Subscription, NeverEndingItems(), cancellationToken: cts.Token))
            {
            }
        };

        await act.Should().ThrowAsync<AccessDeniedException>();
        ran.Should().BeTrue();
    }

    private static async IAsyncEnumerable<int> DelayedItems(params int[] values)
    {
        foreach (var value in values)
        {
            await Task.Yield();
            yield return value;
        }
    }

    private static async IAsyncEnumerable<int> NeverEndingItems([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            yield return 0;
        }
    }

    private sealed class StubPdp(AuthorizationDecision decision, bool kept = false) : IPolicyDecisionPoint
    {
        public Task<AuthorizationDecision> DecideOnceAsync(AuthorizationSubscription s, CancellationToken c = default) =>
            Task.FromResult(decision);

        public async IAsyncEnumerable<AuthorizationDecision> Decide(
            AuthorizationSubscription s,
            [EnumeratorCancellation] CancellationToken c = default)
        {
            yield return decision;
            if (kept)
            {
                await Task.Delay(Timeout.Infinite, c).ConfigureAwait(false);
            }
        }

        public Task<MultiAuthorizationDecision> MultiDecideAllOnceAsync(MultiAuthorizationSubscription s, CancellationToken c = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<IdentifiableAuthorizationDecision> MultiDecide(MultiAuthorizationSubscription s, CancellationToken c = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<MultiAuthorizationDecision> MultiDecideAll(MultiAuthorizationSubscription s, CancellationToken c = default) =>
            throw new NotSupportedException();
    }

    private sealed class RunnerProvider(string type, Action onRun) : IConstraintHandlerProvider
    {
        public IReadOnlyList<ScopedHandler> GetConstraintHandlers(JsonElement constraint, IReadOnlySet<SignalType> supportedSignals) =>
            IConstraintHandlerProvider.ConstraintIsOfType(constraint, type)
                ? [new ScopedHandler(new ConstraintHandler.Runner(onRun), SignalType.Decision, 0)]
                : [];
    }

    private sealed class UppercaseProvider : IConstraintHandlerProvider
    {
        public IReadOnlyList<ScopedHandler> GetConstraintHandlers(JsonElement constraint, IReadOnlySet<SignalType> supportedSignals)
        {
            if (!IConstraintHandlerProvider.ConstraintIsOfType(constraint, "uppercase"))
            {
                return [];
            }

            var output = supportedSignals.First(signal => signal.Kind == SignalKind.Output);
            return [new ScopedHandler(new ConstraintHandler.Mapper(value => ((string)value!).ToUpperInvariant()), output, 0)];
        }
    }
}
