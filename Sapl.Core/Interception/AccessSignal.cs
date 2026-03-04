namespace Sapl.Core.Interception;

public enum AccessSignalKind
{
    Denied,
    Recovered,
}

public sealed record AccessSignal(AccessSignalKind Kind, string Message)
{
    public static AccessSignal Denied(string message = "Access denied by policy")
        => new(AccessSignalKind.Denied, message);

    public static AccessSignal Recovered(string message = "Access recovered")
        => new(AccessSignalKind.Recovered, message);
}
