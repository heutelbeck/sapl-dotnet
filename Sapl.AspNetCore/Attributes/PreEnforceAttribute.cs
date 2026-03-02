namespace Sapl.AspNetCore.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class PreEnforceAttribute : Attribute
{
    public string? Subject { get; set; }

    public string? Action { get; set; }

    public string? Resource { get; set; }

    public string? Environment { get; set; }
}
