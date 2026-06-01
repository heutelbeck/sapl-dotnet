namespace Sapl.Core.Attributes;

/// <summary>
/// Runs the protected method, then decides on its result and returns it transformed by
/// output obligations, or throws on denial. The result is the subscription resource
/// unless <see cref="Resource"/> overrides it.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class PostEnforceAttribute : Attribute
{
    public string? Subject { get; set; }

    public string? Action { get; set; }

    public string? Resource { get; set; }

    public string? Environment { get; set; }

    public string? Secrets { get; set; }

    /// <summary>Type implementing ISubscriptionCustomizer to shape the subscription beyond constant attribute values.</summary>
    public Type? Customizer { get; set; }
}
