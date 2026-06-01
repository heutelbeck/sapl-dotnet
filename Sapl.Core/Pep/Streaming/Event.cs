using Sapl.Core.Authorization;
using Sapl.Core.Pep.Constraints;

namespace Sapl.Core.Pep.Streaming;

/// <summary>
/// The input alphabet of the streaming PEP's Mealy machine: PDP-side decision
/// events, RAP-side stream events, and downstream subscriber lifecycle events.
/// The pipeline pre-classifies raw PDP decisions into the verb-specific cases.
/// </summary>
public abstract record Event
{
    private Event()
    {
    }

    /// <summary>PERMIT, and decision-scoped enforcement succeeded.</summary>
    public sealed record PdpPermit(AuthorizationDecision Decision, EnforcementPlan Plan) : Event;

    /// <summary>An explicit SUSPEND from the PDP.</summary>
    public sealed record PdpSuspend(AuthorizationDecision Decision, EnforcementPlan Plan, TransitionReason Reason) : Event;

    /// <summary>
    /// Access is denied: an explicit DENY, INDETERMINATE or NOT_APPLICABLE under
    /// strict fail-closed, or a PERMIT whose decision-scoped enforcement failed.
    /// <see cref="DenyKind"/> discriminates the cause.
    /// </summary>
    public sealed record PdpDeny(AuthorizationDecision Decision, EnforcementPlan Plan, DenyKind Kind) : Event;

    /// <summary>The PDP's decision stream raised. Terminal.</summary>
    public sealed record PdpError(Exception Error) : Event;

    /// <summary>
    /// The protected method emitted an item. Per-item enforcement has already been
    /// attempted. <paramref name="EnforcementResult"/> carries the post-mapper value
    /// and the failure flag.
    /// </summary>
    public sealed record RapItem(object? Payload, EnforcementResult<object?> EnforcementResult) : Event;

    /// <summary>The protected method (or the wrapping pipeline) raised. Terminal.</summary>
    public sealed record RapError(Exception Error) : Event;

    /// <summary>The protected method completed normally. Terminal.</summary>
    public sealed record RapComplete : Event
    {
        public static readonly RapComplete Instance = new();
    }

    /// <summary>The downstream subscriber canceled. Terminal.</summary>
    public sealed record Cancel : Event
    {
        public static readonly Cancel Instance = new();
    }
}

/// <summary>
/// Discriminator for <see cref="Event.PdpDeny"/>. The four causes are isomorphic
/// from the machine's perspective (each terminates the subscription); the kind
/// selects the denial message and audit diagnostics.
/// </summary>
public enum DenyKind
{
    /// <summary>INDETERMINATE from the PDP.</summary>
    Indeterminate,

    /// <summary>NOT_APPLICABLE from the PDP.</summary>
    NoPolicyApplicable,

    /// <summary>PERMIT, but the plan's decision-scoped enforcement failed.</summary>
    PermitNotEnforceable,

    /// <summary>An explicit DENY from the PDP.</summary>
    PolicyDenied,
}
