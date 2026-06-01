using System.Text.Json;
using FluentAssertions;
using Sapl.Core.Authorization;
using Sapl.Core.Pep;
using Sapl.Core.Pep.Constraints;
using Xunit;

namespace Sapl.Core.Tests.Pep.Constraints;

public sealed class EnforcementPlannerTests
{
    private static readonly SignalType OutputString = SignalType.Output(typeof(string));

    private static JsonElement Constraint(object value) => JsonSerializer.SerializeToElement(value);

    private static IReadOnlySet<SignalType> Supports(params SignalType[] signals) => new HashSet<SignalType>(signals);

    private static AuthorizationDecision WithObligation(JsonElement obligation) =>
        new() { Decision = Decision.Permit, Obligations = [obligation] };

    private sealed class StubProvider(Func<JsonElement, IReadOnlyList<ScopedHandler>> claim) : IConstraintHandlerProvider
    {
        public IReadOnlyList<ScopedHandler> GetConstraintHandlers(
            JsonElement constraint,
            IReadOnlySet<SignalType> supportedSignals) => claim(constraint);
    }

    private static EnforcementResult<object?> ExecuteDecision(EnforcementPlan plan, AuthorizationDecision decision) =>
        plan.Execute(new Signal.Decision(decision), false);

    [Fact]
    public void UnresolvedObligationBecomesADenyingDecisionRunner()
    {
        var decision = WithObligation(Constraint(new { type = "x" }));
        var planner = new EnforcementPlanner([]);

        var plan = planner.Plan(decision, Supports(SignalType.Decision));

        ExecuteDecision(plan, decision).FailureState.Should().BeTrue();
    }

    [Fact]
    public void AmbiguousObligationBecomesADenyingDecisionRunner()
    {
        ScopedHandler Runner(JsonElement _) =>
            new(new ConstraintHandler.Runner(() => { }), SignalType.Decision, 0);
        var decision = WithObligation(Constraint(new { type = "x" }));
        var planner = new EnforcementPlanner(
        [
            new StubProvider(c => [Runner(c)]),
            new StubProvider(c => [Runner(c)]),
        ]);

        var plan = planner.Plan(decision, Supports(SignalType.Decision));

        ExecuteDecision(plan, decision).FailureState.Should().BeTrue();
    }

    [Fact]
    public void AdmissibleHandlerIsScheduledAtItsSignal()
    {
        var ran = false;
        var provider = new StubProvider(_ =>
            [new ScopedHandler(new ConstraintHandler.Runner(() => ran = true), SignalType.Decision, 0)]);
        var decision = WithObligation(Constraint(new { type = "log" }));
        var planner = new EnforcementPlanner([provider]);

        var plan = planner.Plan(decision, Supports(SignalType.Decision));
        ExecuteDecision(plan, decision);

        ran.Should().BeTrue();
    }

    [Fact]
    public void HandlerAtAnUnsupportedSignalIsInadmissibleAndDenies()
    {
        var provider = new StubProvider(_ =>
            [new ScopedHandler(new ConstraintHandler.Runner(() => { }), OutputString, 0)]);
        var decision = WithObligation(Constraint(new { type = "x" }));
        var planner = new EnforcementPlanner([provider]);

        var plan = planner.Plan(decision, Supports(SignalType.Decision));

        ExecuteDecision(plan, decision).FailureState.Should().BeTrue();
    }

    [Fact]
    public void MapperOnAdviceIsInadmissibleButDoesNotDeny()
    {
        var provider = new StubProvider(_ =>
            [new ScopedHandler(new ConstraintHandler.Mapper(v => v), OutputString, 0)]);
        var decision = new AuthorizationDecision { Decision = Decision.Permit, Advice = [Constraint(new { type = "x" })] };
        var planner = new EnforcementPlanner([provider]);

        var plan = planner.Plan(decision, Supports(SignalType.Decision, OutputString));

        ExecuteDecision(plan, decision).FailureState.Should().BeFalse();
    }

    [Fact]
    public void SamexPriorityMapperGroupIsReplacedByDenyingRunners()
    {
        var provider = new StubProvider(_ =>
        [
            new ScopedHandler(new ConstraintHandler.Mapper(v => v), OutputString, 5),
            new ScopedHandler(new ConstraintHandler.Mapper(v => v), OutputString, 5),
        ]);
        var decision = WithObligation(Constraint(new { type = "x" }));
        var planner = new EnforcementPlanner([provider]);

        var plan = planner.Plan(decision, Supports(OutputString));
        var result = plan.Execute(new Signal.Output(typeof(string), Maybe<object?>.Of("v")), false);

        result.FailureState.Should().BeTrue();
    }

    [Fact]
    public void ResourceAddsAnImplicitOutputMapperThatSubstitutesTheResource()
    {
        var decision = new AuthorizationDecision { Decision = Decision.Permit, Resource = Constraint("substituted") };
        var planner = new EnforcementPlanner([]);

        var plan = planner.Plan(decision, Supports(OutputString));
        var result = plan.Execute(new Signal.Output(typeof(string), Maybe<object?>.Of("original")), false);

        result.FailureState.Should().BeFalse();
        result.Value.Should().BeOfType<Maybe<object?>.Present>().Which.Value.Should().Be("substituted");
    }

    [Fact]
    public void ResourceWithoutAnOutputSignalDenies()
    {
        var decision = new AuthorizationDecision { Decision = Decision.Permit, Resource = Constraint("x") };
        var planner = new EnforcementPlanner([]);

        var plan = planner.Plan(decision, Supports(SignalType.Decision));

        ExecuteDecision(plan, decision).FailureState.Should().BeTrue();
    }
}
