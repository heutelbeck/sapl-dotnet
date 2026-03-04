namespace Sapl.Core.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class EnforceRecoverableIfDeniedAttribute : Attribute
{
    public string? Subject { get; set; }

    public string? Action { get; set; }

    public string? Resource { get; set; }

    public string? Environment { get; set; }

    public string? Secrets { get; set; }

    public Type? Customizer { get; set; }
}
