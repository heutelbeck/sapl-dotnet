using FluentAssertions;
using Sapl.Core.Authorization;
using Sapl.Core.Constraints;
using Sapl.Core.Pep;
using Sapl.Core.Pep.Constraints;
using Sapl.Core.Pep.Streaming;
using Xunit;

namespace Sapl.Core.Tests.Pep.Streaming;

public sealed class MealyMachineTests
{
    private static readonly EnforcementPlan Plan = EnforcementPlan.Empty;
    private static readonly AuthorizationDecision PermitDecision = AuthorizationDecision.PermitInstance;
    private static readonly AuthorizationDecision SuspendDecision = new() { Decision = Decision.Suspend };
    private static readonly AuthorizationDecision DenyDecision = AuthorizationDecision.DenyInstance;

    private static State[] NonTerminalStates() =>
        [State.Pending.Instance, new State.Permitting(Plan), State.Suspended.Instance];

    private static Event.PdpPermit Permit() => new(PermitDecision, Plan);

    private static Event.PdpSuspend Suspend() =>
        new(SuspendDecision, Plan, new TransitionReason.Suspended(SuspendDecision));

    private static Event.PdpDeny Deny(DenyKind kind = DenyKind.PolicyDenied) => new(DenyDecision, Plan, kind);

    private static Event.RapItem ItemPresent(object? value) =>
        new(value, new EnforcementResult<object?>(Maybe<object?>.Of(value), false));

    private static Event.RapItem ItemAbsent() =>
        new(null, new EnforcementResult<object?>(Maybe<object?>.Absent.Instance, false));

    private static Event.RapItem ItemFailed() =>
        new(null, new EnforcementResult<object?>(Maybe<object?>.Absent.Instance, true));

    [Fact]
    public void TerminatedAbsorbsEveryEventWithNoEmission()
    {
        Event[] events =
        [
            Permit(), Suspend(), Deny(), new Event.PdpError(new Exception()),
            ItemPresent("x"), ItemFailed(), new Event.RapError(new Exception()),
            Event.RapComplete.Instance, Event.Cancel.Instance,
        ];

        foreach (var evt in events)
        {
            var result = MealyMachine.Step(State.Terminated.Instance, evt);
            result.NewState.Should().BeOfType<State.Terminated>();
            result.Emissions.Should().BeEmpty();
        }
    }

    [Fact]
    public void CancelTerminatesSilently()
    {
        foreach (var state in NonTerminalStates())
        {
            var result = MealyMachine.Step(state, Event.Cancel.Instance);
            result.NewState.Should().BeOfType<State.Terminated>();
            result.Emissions.Should().BeEmpty();
        }
    }

    [Fact]
    public void RapCompleteTerminatesWithComplete()
    {
        var result = MealyMachine.Step(new State.Permitting(Plan), Event.RapComplete.Instance);
        result.IsTerminal.Should().BeTrue();
        result.Emissions.Should().ContainSingle().Which.Should().BeOfType<Emission.EmitComplete>();
    }

    [Fact]
    public void RapErrorTerminatesWithThatError()
    {
        var error = new InvalidOperationException("boom");
        var result = MealyMachine.Step(new State.Permitting(Plan), new Event.RapError(error));
        result.NewState.Should().BeOfType<State.Terminated>();
        result.Emissions.Should().ContainSingle().Which.Should().BeOfType<Emission.EmitError>()
            .Which.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void PdpErrorTerminatesWithThatError()
    {
        var error = new InvalidOperationException("pdp down");
        var result = MealyMachine.Step(State.Pending.Instance, new Event.PdpError(error));
        result.NewState.Should().BeOfType<State.Terminated>();
        result.Emissions.Should().ContainSingle().Which.Should().BeOfType<Emission.EmitError>()
            .Which.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void PermitFromPendingPermitsAndSignalsGranted()
    {
        var result = MealyMachine.Step(State.Pending.Instance, Permit());
        result.NewState.Should().BeOfType<State.Permitting>().Which.Plan.Should().BeSameAs(Plan);
        result.Emissions.Should().ContainSingle().Which.Should().BeOfType<Emission.EmitTransition>()
            .Which.Reason.Should().BeOfType<TransitionReason.Granted>();
    }

    [Fact]
    public void PermitFromSuspendedResumesAndSignalsGranted()
    {
        var result = MealyMachine.Step(State.Suspended.Instance, Permit());
        result.NewState.Should().BeOfType<State.Permitting>();
        result.Emissions.Should().ContainSingle().Which.Should().BeOfType<Emission.EmitTransition>()
            .Which.Reason.Should().BeOfType<TransitionReason.Granted>();
    }

    [Fact]
    public void PermitWhilePermittingReplacesPlanSilently()
    {
        var result = MealyMachine.Step(new State.Permitting(Plan), Permit());
        result.NewState.Should().BeOfType<State.Permitting>();
        result.Emissions.Should().BeEmpty();
    }

    [Fact]
    public void SuspendFromPendingSuspendsAndSignalsTransition()
    {
        var result = MealyMachine.Step(State.Pending.Instance, Suspend());
        result.NewState.Should().BeOfType<State.Suspended>();
        result.Emissions.Should().ContainSingle().Which.Should().BeOfType<Emission.EmitTransition>()
            .Which.Reason.Should().BeOfType<TransitionReason.Suspended>();
    }

    [Fact]
    public void SuspendFromPermittingSuspendsAndSignalsTransition()
    {
        var result = MealyMachine.Step(new State.Permitting(Plan), Suspend());
        result.NewState.Should().BeOfType<State.Suspended>();
        result.Emissions.Should().ContainSingle().Which.Should().BeOfType<Emission.EmitTransition>();
    }

    [Fact]
    public void SuspendWhileSuspendedIsSilent()
    {
        var result = MealyMachine.Step(State.Suspended.Instance, Suspend());
        result.NewState.Should().BeOfType<State.Suspended>();
        result.Emissions.Should().BeEmpty();
    }

    [Fact]
    public void DenyFromAnyNonTerminalStateTerminatesWithAccessDenied()
    {
        foreach (var state in NonTerminalStates())
        {
            var result = MealyMachine.Step(state, Deny());
            result.IsTerminal.Should().BeTrue();
            result.Emissions.Should().ContainSingle().Which.Should().BeOfType<Emission.EmitError>()
                .Which.Error.Should().BeOfType<AccessDeniedException>();
        }
    }

    [Theory]
    [InlineData(DenyKind.PolicyDenied, MealyMachine.DeniedByPolicy)]
    [InlineData(DenyKind.Indeterminate, MealyMachine.DeniedIndeterminate)]
    [InlineData(DenyKind.NoPolicyApplicable, MealyMachine.DeniedNoPolicyApplicable)]
    [InlineData(DenyKind.PermitNotEnforceable, MealyMachine.DeniedPermitNotEnforceable)]
    public void DenyKindSelectsTheDenialMessage(DenyKind kind, string expectedMessage)
    {
        var result = MealyMachine.Step(State.Pending.Instance, Deny(kind));
        var error = result.Emissions.Should().ContainSingle().Which.Should().BeOfType<Emission.EmitError>().Subject.Error;
        error.Should().BeOfType<AccessDeniedException>().Which.Message.Should().Be(expectedMessage);
    }

    [Fact]
    public void ItemWithFailedEnforcementTerminatesFromAnyNonTerminalState()
    {
        foreach (var state in NonTerminalStates())
        {
            var result = MealyMachine.Step(state, ItemFailed());
            result.IsTerminal.Should().BeTrue();
            var error = result.Emissions.Should().ContainSingle().Which.Should().BeOfType<Emission.EmitError>()
                .Subject.Error;
            error.Should().BeOfType<AccessDeniedException>().Which.Message
                .Should().Be(MealyMachine.DeniedByObligationFailure);
        }
    }

    [Fact]
    public void PresentItemWhilePermittingIsEmitted()
    {
        var result = MealyMachine.Step(new State.Permitting(Plan), ItemPresent("payload"));
        result.NewState.Should().BeOfType<State.Permitting>();
        result.Emissions.Should().ContainSingle().Which.Should().BeOfType<Emission.Emit>()
            .Which.Value.Should().Be("payload");
    }

    [Fact]
    public void AbsentItemWhilePermittingIsDroppedSilently()
    {
        var result = MealyMachine.Step(new State.Permitting(Plan), ItemAbsent());
        result.NewState.Should().BeOfType<State.Permitting>();
        result.Emissions.Should().BeEmpty();
    }

    [Fact]
    public void ItemWhilePendingIsDroppedSilently()
    {
        var result = MealyMachine.Step(State.Pending.Instance, ItemPresent("early"));
        result.NewState.Should().BeOfType<State.Pending>();
        result.Emissions.Should().BeEmpty();
    }

    [Fact]
    public void ItemWhileSuspendedIsDroppedSilently()
    {
        var result = MealyMachine.Step(State.Suspended.Instance, ItemPresent("masked"));
        result.NewState.Should().BeOfType<State.Suspended>();
        result.Emissions.Should().BeEmpty();
    }

    [Fact]
    public void IsTerminalHoldsExactlyForTerminatedNewState()
    {
        MealyMachine.Step(State.Pending.Instance, Permit()).IsTerminal.Should().BeFalse();
        MealyMachine.Step(State.Pending.Instance, Suspend()).IsTerminal.Should().BeFalse();
        MealyMachine.Step(State.Pending.Instance, Deny()).IsTerminal.Should().BeTrue();
        MealyMachine.Step(State.Pending.Instance, Event.Cancel.Instance).IsTerminal.Should().BeTrue();
    }
}
