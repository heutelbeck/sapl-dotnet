using Sapl.Core.Authorization;

namespace Sapl.Core.Pep.Streaming;

/// <summary>
/// The output alphabet of the streaming PEP's Mealy machine: what the machine
/// asks the downstream adapter to deliver on a single transition. A step may
/// produce zero, one, or several emissions.
/// </summary>
public abstract record Emission
{
    private Emission()
    {
    }

    /// <summary>Deliver <paramref name="Value"/> to the subscriber.</summary>
    public sealed record Emit(object? Value) : Emission;

    /// <summary>Terminate the subscriber with an error.</summary>
    public sealed record EmitError(Exception Error) : Emission;

    /// <summary>Terminate the subscriber normally.</summary>
    public sealed record EmitComplete : Emission
    {
        public static readonly EmitComplete Instance = new();
    }

    /// <summary>Deliver an out-of-band suspend/resume boundary signal.</summary>
    public sealed record EmitTransition(TransitionReason Reason) : Emission;
}

/// <summary>
/// Why the machine crossed a state boundary. Carried by
/// <see cref="Emission.EmitTransition"/> so subscribers can react, and used to
/// format denial messages.
/// </summary>
public abstract record TransitionReason
{
    private TransitionReason()
    {
    }

    /// <summary>Suspended by an explicit SUSPEND from the PDP.</summary>
    public sealed record Suspended(AuthorizationDecision Decision) : TransitionReason;

    /// <summary>
    /// Entered or resumed permitting state (initial grant or resume from suspended).
    /// Plan replacement while already permitting is silent.
    /// </summary>
    public sealed record Granted(AuthorizationDecision Decision) : TransitionReason;
}
