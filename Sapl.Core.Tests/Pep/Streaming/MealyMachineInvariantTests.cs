using FluentAssertions;
using Sapl.Core.Authorization;
using Sapl.Core.Constraints;
using Sapl.Core.Pep;
using Sapl.Core.Pep.Constraints;
using Sapl.Core.Pep.Streaming;
using Xunit;

namespace Sapl.Core.Tests.Pep.Streaming;

/// <summary>
/// The streaming automaton's universally-quantified invariants, expressed discretely
/// (one theorem each) and quantified over every non-terminal state, mirroring the
/// invariant suites of the other PEPs. <see cref="MealyMachineTests"/> covers the
/// per-cell transition table; this file states the laws that must hold across all
/// states.
/// </summary>
public sealed class MealyMachineInvariantTests
{
    private static readonly EnforcementPlan Plan = EnforcementPlan.Empty;
    private static readonly AuthorizationDecision PermitDecision = AuthorizationDecision.PermitInstance;
    private static readonly AuthorizationDecision SuspendDecision = new() { Decision = Decision.Suspend };
    private static readonly AuthorizationDecision DenyDecision = AuthorizationDecision.DenyInstance;

    public static IEnumerable<object[]> NonTerminalStates() =>
    [
        [State.Pending.Instance],
        [new State.Permitting(Plan)],
        [State.Suspended.Instance],
    ];

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

    // 1. DENY is universally terminal with an access-denied error.
    [Theory]
    [MemberData(nameof(NonTerminalStates))]
    public void DenyAlwaysTerminatesWithAccessDenied(State state)
    {
        var result = MealyMachine.Step(state, Deny());
        result.IsTerminal.Should().BeTrue();
        result.Emissions.Should().ContainSingle().Which.Should().BeOfType<Emission.EmitError>()
            .Which.Error.Should().BeOfType<AccessDeniedException>();
    }

    // 2. A PDP error is universally terminal and carries that error.
    [Theory]
    [MemberData(nameof(NonTerminalStates))]
    public void PdpErrorAlwaysTerminatesWithThatError(State state)
    {
        var error = new InvalidOperationException("pdp");
        var result = MealyMachine.Step(state, new Event.PdpError(error));
        result.IsTerminal.Should().BeTrue();
        result.Emissions.Should().ContainSingle().Which.Should().BeOfType<Emission.EmitError>()
            .Which.Error.Should().BeSameAs(error);
    }

    // 3. A RAP error is universally terminal and carries that error.
    [Theory]
    [MemberData(nameof(NonTerminalStates))]
    public void RapErrorAlwaysTerminatesWithThatError(State state)
    {
        var error = new InvalidOperationException("rap");
        var result = MealyMachine.Step(state, new Event.RapError(error));
        result.IsTerminal.Should().BeTrue();
        result.Emissions.Should().ContainSingle().Which.Should().BeOfType<Emission.EmitError>()
            .Which.Error.Should().BeSameAs(error);
    }

    // 4. Cancel is universally terminal and silent.
    [Theory]
    [MemberData(nameof(NonTerminalStates))]
    public void CancelAlwaysTerminatesSilently(State state)
    {
        var result = MealyMachine.Step(state, Event.Cancel.Instance);
        result.IsTerminal.Should().BeTrue();
        result.Emissions.Should().BeEmpty();
    }

    // 5. RAP completion is universally terminal with a complete emission.
    [Theory]
    [MemberData(nameof(NonTerminalStates))]
    public void RapCompleteAlwaysTerminatesWithComplete(State state)
    {
        var result = MealyMachine.Step(state, Event.RapComplete.Instance);
        result.IsTerminal.Should().BeTrue();
        result.Emissions.Should().ContainSingle().Which.Should().BeOfType<Emission.EmitComplete>();
    }

    // 6. A per-item enforcement failure is universally terminal (obligation-failure denial).
    [Theory]
    [MemberData(nameof(NonTerminalStates))]
    public void FailedItemAlwaysTerminatesWithObligationFailure(State state)
    {
        var result = MealyMachine.Step(state, ItemFailed());
        result.IsTerminal.Should().BeTrue();
        result.Emissions.Should().ContainSingle().Which.Should().BeOfType<Emission.EmitError>()
            .Which.Error.Should().BeOfType<AccessDeniedException>()
            .Which.Message.Should().Be(MealyMachine.DeniedByObligationFailure);
    }

    // 7. A terminated machine absorbs every event with no emission.
    [Fact]
    public void TerminatedAbsorbsEveryEvent()
    {
        Event[] events =
        [
            Permit(), Suspend(), Deny(), new Event.PdpError(new Exception()), ItemPresent("x"),
            ItemAbsent(), ItemFailed(), new Event.RapError(new Exception()),
            Event.RapComplete.Instance, Event.Cancel.Instance,
        ];
        foreach (var evt in events)
        {
            var result = MealyMachine.Step(State.Terminated.Instance, evt);
            result.NewState.Should().BeOfType<State.Terminated>();
            result.Emissions.Should().BeEmpty();
        }
    }

    // 8. A present item is emitted only while permitting; dropped in Pending and Suspended.
    [Theory]
    [MemberData(nameof(NonTerminalStates))]
    public void PresentItemEmittedOnlyWhilePermitting(State state)
    {
        var result = MealyMachine.Step(state, ItemPresent("payload"));
        result.IsTerminal.Should().BeFalse();
        if (state is State.Permitting)
        {
            result.Emissions.Should().ContainSingle().Which.Should().BeOfType<Emission.Emit>()
                .Which.Value.Should().Be("payload");
        }
        else
        {
            result.Emissions.Should().BeEmpty();
        }
    }

    // 9. An absent item is universally dropped silently (no emission, non-terminal).
    [Theory]
    [MemberData(nameof(NonTerminalStates))]
    public void AbsentItemAlwaysDroppedSilently(State state)
    {
        var result = MealyMachine.Step(state, ItemAbsent());
        result.IsTerminal.Should().BeFalse();
        result.Emissions.Should().BeEmpty();
    }

    // 10. The initial grant and a resume both emit a Granted transition.
    [Fact]
    public void InitialGrantAndResumeBothEmitGranted()
    {
        foreach (var state in new State[] { State.Pending.Instance, State.Suspended.Instance })
        {
            var result = MealyMachine.Step(state, Permit());
            result.NewState.Should().BeOfType<State.Permitting>();
            result.Emissions.Should().ContainSingle().Which.Should().BeOfType<Emission.EmitTransition>()
                .Which.Reason.Should().BeOfType<TransitionReason.Granted>();
        }
    }

    // 11. A re-permit while already permitting replaces the plan silently.
    [Fact]
    public void RePermitWhilePermittingIsSilent()
    {
        var result = MealyMachine.Step(new State.Permitting(Plan), Permit());
        result.NewState.Should().BeOfType<State.Permitting>();
        result.Emissions.Should().BeEmpty();
    }

    // 12. A suspend from Pending or Permitting emits a Suspended transition.
    [Fact]
    public void SuspendFromPendingOrPermittingEmitsSuspended()
    {
        foreach (var state in new State[] { State.Pending.Instance, new State.Permitting(Plan) })
        {
            var result = MealyMachine.Step(state, Suspend());
            result.NewState.Should().BeOfType<State.Suspended>();
            result.Emissions.Should().ContainSingle().Which.Should().BeOfType<Emission.EmitTransition>()
                .Which.Reason.Should().BeOfType<TransitionReason.Suspended>();
        }
    }

    // 13. A re-suspend while already suspended is silent.
    [Fact]
    public void ReSuspendWhileSuspendedIsSilent()
    {
        var result = MealyMachine.Step(State.Suspended.Instance, Suspend());
        result.NewState.Should().BeOfType<State.Suspended>();
        result.Emissions.Should().BeEmpty();
    }

    // 14. The deny kind selects the denial message, from any non-terminal state.
    [Theory]
    [InlineData(DenyKind.PolicyDenied, MealyMachine.DeniedByPolicy)]
    [InlineData(DenyKind.Indeterminate, MealyMachine.DeniedIndeterminate)]
    [InlineData(DenyKind.NoPolicyApplicable, MealyMachine.DeniedNoPolicyApplicable)]
    [InlineData(DenyKind.PermitNotEnforceable, MealyMachine.DeniedPermitNotEnforceable)]
    public void DenyKindSelectsTheDenialMessage(DenyKind kind, string expectedMessage)
    {
        foreach (var state in new[] { State.Pending.Instance, new State.Permitting(Plan), (State)State.Suspended.Instance })
        {
            var result = MealyMachine.Step(state, Deny(kind));
            result.Emissions.Should().ContainSingle().Which.Should().BeOfType<Emission.EmitError>()
                .Which.Error.Should().BeOfType<AccessDeniedException>().Which.Message.Should().Be(expectedMessage);
        }
    }

    // 15. IsTerminal holds exactly when the new state is Terminated.
    [Fact]
    public void IsTerminalHoldsExactlyForTerminatedNewState()
    {
        MealyMachine.Step(State.Pending.Instance, Permit()).IsTerminal.Should().BeFalse();
        MealyMachine.Step(State.Pending.Instance, Suspend()).IsTerminal.Should().BeFalse();
        MealyMachine.Step(new State.Permitting(Plan), ItemPresent("x")).IsTerminal.Should().BeFalse();
        MealyMachine.Step(State.Pending.Instance, Deny()).IsTerminal.Should().BeTrue();
        MealyMachine.Step(State.Pending.Instance, Event.Cancel.Instance).IsTerminal.Should().BeTrue();
    }
}
