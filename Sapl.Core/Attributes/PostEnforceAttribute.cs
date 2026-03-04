namespace Sapl.Core.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class PostEnforceAttribute : Attribute
{
    public string? Subject { get; set; }

    public string? Action { get; set; }

    public string? Resource { get; set; }

    public string? Environment { get; set; }

    public string? Secrets { get; set; }

    public Type? Customizer { get; set; }
}
