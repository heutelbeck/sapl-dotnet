using System.Text.Json;
using FluentAssertions;
using Sapl.Core.Authorization;
using Sapl.Core.Pep;
using Sapl.Core.Pep.Constraints;
using Xunit;

namespace Sapl.Core.Tests.Pep.Constraints;

// A constraint handler provider may fail while resolving a malformed constraint (for
// example an obligation whose condition compiles an invalid regex). Spring treats such a
// throwing provider as a no-claim so the constraint fails closed through the unresolved
// substitute, rather than letting the raw exception escape planning. (F1)
public sealed class EnforcementPlannerProviderFailureTests
{
    private static readonly SignalType OutputString = SignalType.Output(typeof(string));

    private static JsonElement Constraint(object value) => JsonSerializer.SerializeToElement(value);

    private static IReadOnlySet<SignalType> Supports(params SignalType[] signals) => new HashSet<SignalType>(signals);

    private static AuthorizationDecision WithObligation(JsonElement obligation) =>
        new() { Decision = Decision.Permit, Obligations = [obligation] };

    private static EnforcementResult<object?> ExecuteDecision(EnforcementPlan plan, AuthorizationDecision decision) =>
        plan.Execute(new Signal.Decision(decision), false);

    private sealed class ThrowingProvider(Exception failure) : IConstraintHandlerProvider
    {
        public IReadOnlyList<ScopedHandler> GetConstraintHandlers(
            JsonElement constraint,
            IReadOnlySet<SignalType> supportedSignals) => throw failure;
    }

    private sealed class StubProvider(Func<JsonElement, IReadOnlyList<ScopedHandler>> claim) : IConstraintHandlerProvider
    {
        public IReadOnlyList<ScopedHandler> GetConstraintHandlers(
            JsonElement constraint,
            IReadOnlySet<SignalType> supportedSignals) => claim(constraint);
    }

    [Fact]
    public void ProviderThatThrowsWhileResolvingDoesNotEscapePlanning()
    {
        var decision = WithObligation(Constraint(new { type = "filterJsonContent" }));
        var planner = new EnforcementPlanner([new ThrowingProvider(new InvalidOperationException("invalid regex"))]);

        var act = () => planner.Plan(decision, Supports(SignalType.Decision));

        act.Should().NotThrow();
    }

    [Fact]
    public void ProviderThatThrowsWhileResolvingFailsClosedAsAnUnresolvedObligation()
    {
        var decision = WithObligation(Constraint(new { type = "filterJsonContent" }));
        var planner = new EnforcementPlanner([new ThrowingProvider(new InvalidOperationException("invalid regex"))]);

        var plan = planner.Plan(decision, Supports(SignalType.Decision));

        ExecuteDecision(plan, decision).FailureState.Should().BeTrue();
    }

    [Fact]
    public void ThrowingProviderIsTreatedAsNoClaimSoAnotherProviderStillResolvesTheConstraint()
    {
        var ran = false;
        var working = new StubProvider(_ =>
            [new ScopedHandler(new ConstraintHandler.Runner(() => ran = true), SignalType.Decision, 0)]);
        var decision = WithObligation(Constraint(new { type = "log" }));
        var planner = new EnforcementPlanner(
        [
            new ThrowingProvider(new InvalidOperationException("provider bug")),
            working,
        ]);

        var plan = planner.Plan(decision, Supports(SignalType.Decision));
        ExecuteDecision(plan, decision);

        ran.Should().BeTrue();
    }
}
