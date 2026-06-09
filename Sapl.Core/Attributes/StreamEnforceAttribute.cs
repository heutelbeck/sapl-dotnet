namespace Sapl.Core.Attributes;

/// <summary>
/// Enforces a streaming endpoint. The PDP decision stream and the protected stream are
/// combined: permitted items flow (transformed by output obligations), an explicit
/// SUSPEND drops items until the next permit, and any denial terminates the stream.
/// Routing is driven by the decision verb, not by a per-mode flag.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class StreamEnforceAttribute : Attribute
{
    public string? Subject { get; set; }

    public string? Action { get; set; }

    public string? Resource { get; set; }

    public string? Environment { get; set; }

    public string? Secrets { get; set; }

    /// <summary>Type implementing ISubscriptionCustomizer to shape the subscription beyond constant attribute values.</summary>
    public Type? Customizer { get; set; }

    /// <summary>
    /// When true, each suspend/resume boundary crossing surfaces an out-of-band transition
    /// frame to the subscriber (ACCESS_SUSPENDED / ACCESS_GRANTED). When false (default)
    /// transitions are silent and items simply drop while suspended. Honoured by the SSE
    /// controller filter; the domain proxy path ignores it.
    /// </summary>
    public bool SignalTransitions { get; set; }

    /// <summary>
    /// When true, the protected source is not pulled while the stream is suspended: it pauses
    /// on entry to Suspended and resumes on the next grant, rather than running and having its
    /// items dropped. When false (default) the source keeps producing and items drop silently
    /// while suspended.
    /// </summary>
    public bool PauseRapDuringSuspend { get; set; }
}
