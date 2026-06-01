using System.Diagnostics;
using Sapl.Core.Constraints;
using Sapl.Core.Pep;

namespace Sapl.Core.Pep.Streaming;

/// <summary>
/// The codomain of the machine's step function: the post-step <see cref="State"/>
/// paired with the ordered emissions produced by the step (the Mealy multi-output).
/// An empty emission list means "event processed; nothing to emit downstream."
/// </summary>
public sealed record StepResult(State NewState, IReadOnlyList<Emission> Emissions)
{
    /// <summary>A step into <paramref name="newState"/> producing the given emissions in order.</summary>
    public static StepResult To(State newState, params Emission[] emissions) => new(newState, emissions);

    /// <summary>True when the new state is <see cref="State.Terminated"/>.</summary>
    public bool IsTerminal => NewState is State.Terminated;
}

/// <summary>
/// The streaming PEP's Mealy machine: the pure, total combined transition and
/// output function S x Sigma -&gt; S x Lambda. Routing dispatches on the PDP
/// decision verb carried by the event and the current state. Explicit DENY always
/// terminates, SUSPEND always transitions to <see cref="State.Suspended"/>, and a
/// per-item obligation failure terminates under the strict fail-closed default.
/// No side effects, no I/O, no transport types.
/// </summary>
public static class MealyMachine
{
    /// <summary>Denial message for a per-item obligation handler failure.</summary>
    public const string DeniedByObligationFailure = "Access denied: a per-item obligation handler failed.";

    /// <summary>Denial message for an explicit DENY from the PDP.</summary>
    public const string DeniedByPolicy = "Access denied by policy.";

    /// <summary>Denial message for INDETERMINATE from the PDP.</summary>
    public const string DeniedIndeterminate = "Access denied: policy evaluation produced an indeterminate result.";

    /// <summary>Denial message for NOT_APPLICABLE from the PDP.</summary>
    public const string DeniedNoPolicyApplicable = "Access denied: no applicable policy found.";

    /// <summary>Denial message for a PERMIT whose decision-scoped enforcement failed.</summary>
    public const string DeniedPermitNotEnforceable = "Access denied: decision-scoped enforcement of permit failed.";

    /// <summary>Compute the next state and emissions for a single (state, event) pair.</summary>
    public static StepResult Step(State state, Event evt)
    {
        if (state is State.Terminated terminated)
            return StepResult.To(terminated);

        return evt switch
        {
            Event.Cancel => Terminate(),
            Event.RapComplete => TerminateNormally(),
            Event.RapError rapError => TerminateWithError(rapError.Error),
            Event.PdpError pdpError => TerminateWithError(pdpError.Error),
            Event.PdpPermit permit => OnPermit(state, permit),
            Event.PdpSuspend suspend => OnSuspend(state, suspend),
            Event.PdpDeny deny => OnDeny(deny),
            Event.RapItem item => OnItem(state, item),
            _ => throw new UnreachableException(),
        };
    }

    private static StepResult OnPermit(State state, Event.PdpPermit permit)
    {
        var next = new State.Permitting(permit.Plan);
        // Plan replacement while permitting is silent. Initial grant and resume
        // emit the Granted boundary signal.
        if (state is State.Permitting)
            return StepResult.To(next);
        return StepResult.To(next, new Emission.EmitTransition(new TransitionReason.Granted(permit.Decision)));
    }

    private static StepResult OnSuspend(State state, Event.PdpSuspend suspend)
    {
        // Re-suspend while suspended is silent. The boundary already occurred.
        if (state is State.Suspended)
            return StepResult.To(State.Suspended.Instance);
        return StepResult.To(State.Suspended.Instance, new Emission.EmitTransition(suspend.Reason));
    }

    private static StepResult OnDeny(Event.PdpDeny deny)
    {
        var message = deny.Kind switch
        {
            DenyKind.Indeterminate => DeniedIndeterminate,
            DenyKind.NoPolicyApplicable => DeniedNoPolicyApplicable,
            DenyKind.PermitNotEnforceable => DeniedPermitNotEnforceable,
            DenyKind.PolicyDenied => DeniedByPolicy,
            _ => throw new UnreachableException(),
        };
        return StepResult.To(State.Terminated.Instance, new Emission.EmitError(new AccessDeniedException(message)));
    }

    private static StepResult OnItem(State state, Event.RapItem item)
    {
        // A per-item obligation failure terminates from any non-terminated state.
        if (item.EnforcementResult.FailureState)
            return StepResult.To(
                State.Terminated.Instance,
                new Emission.EmitError(new AccessDeniedException(DeniedByObligationFailure)));

        return state switch
        {
            // No decision yet. Items are dropped silently.
            State.Pending pending => StepResult.To(pending),
            // Suspended. Items are dropped silently.
            State.Suspended suspended => StepResult.To(suspended),
            // Permitting. Present values are delivered, absent values dropped.
            State.Permitting permitting => PermittingItem(permitting, item),
            // Terminated is handled before reaching here.
            _ => throw new UnreachableException(),
        };
    }

    private static StepResult PermittingItem(State.Permitting state, Event.RapItem item)
    {
        if (item.EnforcementResult.Value is Maybe<object?>.Present present)
            return StepResult.To(state, new Emission.Emit(present.Value));
        // Absent means the mapper dropped the item. No observable output.
        return StepResult.To(state);
    }

    private static StepResult Terminate() => StepResult.To(State.Terminated.Instance);

    private static StepResult TerminateNormally() =>
        StepResult.To(State.Terminated.Instance, Emission.EmitComplete.Instance);

    private static StepResult TerminateWithError(Exception error) =>
        StepResult.To(State.Terminated.Instance, new Emission.EmitError(error));
}
