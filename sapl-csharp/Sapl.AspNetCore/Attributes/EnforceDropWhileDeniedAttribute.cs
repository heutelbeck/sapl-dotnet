namespace Sapl.AspNetCore.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class EnforceDropWhileDeniedAttribute : Attribute
{
    public string? Subject { get; set; }

    public string? Action { get; set; }

    public string? Resource { get; set; }

    public string? Environment { get; set; }
}
