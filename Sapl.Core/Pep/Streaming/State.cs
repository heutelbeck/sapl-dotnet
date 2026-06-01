using Sapl.Core.Pep.Constraints;

namespace Sapl.Core.Pep.Streaming;

/// <summary>
/// The state set of the streaming PEP's Mealy machine. Four cases describe the
/// entire lifecycle of one subscription. Routing is driven by the PDP decision
/// verb carried by the event, not by an annotation flag.
/// </summary>
public abstract record State
{
    private State()
    {
    }

    /// <summary>No PDP decision has arrived yet. The pipeline is subscribed to the PDP.</summary>
    public sealed record Pending : State
    {
        public static readonly Pending Instance = new();
    }

    /// <summary>
    /// The current decision permits and <paramref name="Plan"/> is usable. Per-item
    /// enforcement and lifecycle signals run against this plan while it is current.
    /// </summary>
    public sealed record Permitting(EnforcementPlan Plan) : State;

    /// <summary>
    /// The PDP returned an explicit SUSPEND. The subscription is preserved and RAP
    /// items are dropped silently. The next PERMIT resumes to <see cref="Permitting"/>.
    /// Any denial terminates.
    /// </summary>
    public sealed record Suspended : State
    {
        public static readonly Suspended Instance = new();
    }

    /// <summary>
    /// Absorbing state. Reached on RAP completion or error, downstream cancellation,
    /// PDP error, any denial, or a per-item obligation failure. No further events
    /// are processed.
    /// </summary>
    public sealed record Terminated : State
    {
        public static readonly Terminated Instance = new();
    }
}
