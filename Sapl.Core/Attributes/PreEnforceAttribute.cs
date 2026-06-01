namespace Sapl.Core.Attributes;

/// <summary>
/// Decides before the protected method runs and gates it. Denial throws and the host
/// maps it to 403. Each property, when set, overrides the corresponding subscription
/// element; unset elements fall back to host defaults.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class PreEnforceAttribute : Attribute
{
    public string? Subject { get; set; }

    public string? Action { get; set; }

    public string? Resource { get; set; }

    public string? Environment { get; set; }

    public string? Secrets { get; set; }

    /// <summary>Type implementing ISubscriptionCustomizer to shape the subscription beyond constant attribute values.</summary>
    public Type? Customizer { get; set; }
}
