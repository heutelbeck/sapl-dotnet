using FluentAssertions;
using Sapl.Core.Authorization;
using Sapl.Core.Pep;
using Sapl.Core.Pep.Constraints;
using Xunit;

namespace Sapl.Core.Tests.Pep.Constraints;

public sealed class EnforcementPlanTests
{
    private static readonly SignalType OutputString = SignalType.Output(typeof(string));

    private static EnforcementPlanEntry Entry(ConstraintHandler handler, ConstraintType type = ConstraintType.Obligation) =>
        new(handler, 0, type, default);

    private static EnforcementPlan PlanWith(SignalType signal, params EnforcementPlanEntry[] entries) =>
        new(new Dictionary<SignalType, IReadOnlyList<EnforcementPlanEntry>> { [signal] = entries });

    [Fact]
    public void RunnerRunsAndPassesTheValueThrough()
    {
        var ran = false;
        var plan = PlanWith(OutputString, Entry(new ConstraintHandler.Runner(() => ran = true)));

        var result = plan.Execute(new Signal.Output(typeof(string), Maybe<object?>.Of("v")), false);

        ran.Should().BeTrue();
        result.FailureState.Should().BeFalse();
        result.Value.Should().BeOfType<Maybe<object?>.Present>().Which.Value.Should().Be("v");
    }

    [Fact]
    public void MapperTransformsAPresentValue()
    {
        var plan = PlanWith(OutputString, Entry(new ConstraintHandler.Mapper(v => $"{v}!")));

        var result = plan.Execute(new Signal.Output(typeof(string), Maybe<object?>.Of("v")), false);

        result.Value.Should().BeOfType<Maybe<object?>.Present>().Which.Value.Should().Be("v!");
    }

    [Fact]
    public void ConsumerAndMapperAreSkippedForAnAbsentValue()
    {
        var observed = false;
        var plan = PlanWith(
            OutputString,
            Entry(new ConstraintHandler.Mapper(_ => "mapped")),
            Entry(new ConstraintHandler.Consumer(_ => observed = true)));

        var result = plan.Execute(new Signal.Output(typeof(string), Maybe<object?>.Absent.Instance), false);

        observed.Should().BeFalse();
        result.Value.Should().BeOfType<Maybe<object?>.Absent>();
    }

    [Fact]
    public void EntriesThreadTheValueInOrder()
    {
        object? consumed = null;
        var plan = PlanWith(
            OutputString,
            Entry(new ConstraintHandler.Mapper(v => $"{v}-mapped")),
            Entry(new ConstraintHandler.Consumer(v => consumed = v)));

        plan.Execute(new Signal.Output(typeof(string), Maybe<object?>.Of("v")), false);

        consumed.Should().Be("v-mapped");
    }

    [Fact]
    public void ThrowingObligationHandlerFlipsTheFailureState()
    {
        var plan = PlanWith(SignalType.Decision, Entry(new ConstraintHandler.Runner(() => throw new Exception("x"))));

        var result = plan.Execute(new Signal.Decision(AuthorizationDecision.PermitInstance), false);

        result.FailureState.Should().BeTrue();
    }

    [Fact]
    public void ThrowingAdviceHandlerDoesNotFlipTheFailureState()
    {
        var plan = PlanWith(
            SignalType.Decision,
            Entry(new ConstraintHandler.Runner(() => throw new Exception("x")), ConstraintType.Advice));

        var result = plan.Execute(new Signal.Decision(AuthorizationDecision.PermitInstance), false);

        result.FailureState.Should().BeFalse();
    }

    [Fact]
    public void DecisionSignalSeedsTheDecisionAsTheInitialValue()
    {
        object? seen = null;
        var plan = PlanWith(SignalType.Decision, Entry(new ConstraintHandler.Consumer(v => seen = v)));

        plan.Execute(new Signal.Decision(AuthorizationDecision.PermitInstance), false);

        seen.Should().BeSameAs(AuthorizationDecision.PermitInstance);
    }

    [Fact]
    public void VoidSignalSeedsAnAbsentValue()
    {
        object? seen = "untouched";
        var plan = PlanWith(SignalType.Complete, Entry(new ConstraintHandler.Consumer(v => seen = v)));

        var result = plan.Execute(Signal.Complete.Instance, false);

        seen.Should().Be("untouched");
        result.Value.Should().BeOfType<Maybe<object?>.Absent>();
    }

    [Fact]
    public void PriorFailureStatePropagates()
    {
        var result = EnforcementPlan.Empty.Execute(Signal.Complete.Instance, true);

        result.FailureState.Should().BeTrue();
    }
}
