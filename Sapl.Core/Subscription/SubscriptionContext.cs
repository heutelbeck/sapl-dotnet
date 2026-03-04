using System.Security.Claims;

namespace Sapl.Core.Subscription;

public sealed class SubscriptionContext
{
    public ClaimsPrincipal? Principal { get; init; }

    public string? MethodName { get; init; }

    public string? ClassName { get; init; }

    public IDictionary<string, object?>? MethodArguments { get; init; }

    public object? ReturnValue { get; init; }

    public string? BearerToken { get; init; }

    public IDictionary<string, object?> Properties { get; init; }
        = new Dictionary<string, object?>();
}
