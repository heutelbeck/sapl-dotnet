using Microsoft.AspNetCore.Http;

namespace Sapl.AspNetCore.Subscription;

public sealed class SubscriptionContext
{
    public HttpContext? HttpContext { get; init; }

    public string? MethodName { get; init; }

    public string? ClassName { get; init; }

    public IDictionary<string, object?>? MethodArguments { get; init; }

    public object? ReturnValue { get; init; }
}
