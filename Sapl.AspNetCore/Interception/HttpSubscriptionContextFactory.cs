using System.Reflection;
using Microsoft.AspNetCore.Http;
using Sapl.Core.Subscription;

namespace Sapl.AspNetCore.Interception;

public sealed class HttpSubscriptionContextFactory
{
    private readonly IHttpContextAccessor _accessor;

    public HttpSubscriptionContextFactory(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public SubscriptionContext Create(
        MethodInfo method, object?[]? args, object? returnValue = null)
    {
        var httpContext = _accessor.HttpContext;
        var properties = new Dictionary<string, object?>();

        if (httpContext is not null)
        {
            properties["path"] = httpContext.Request.Path.Value;
            properties["httpMethod"] = httpContext.Request.Method;

            var routeValues = httpContext.Request.RouteValues;
            if (routeValues.Count > 0)
            {
                properties["params"] = routeValues.ToDictionary(
                    kv => kv.Key, kv => kv.Value?.ToString());
            }

            var query = httpContext.Request.Query;
            if (query.Count > 0)
            {
                properties["query"] = query.ToDictionary(
                    kv => kv.Key, kv => kv.Value.ToString());
            }
        }

        return new SubscriptionContext
        {
            Principal = httpContext?.User,
            MethodName = method.Name,
            ClassName = method.DeclaringType?.Name,
            MethodArguments = BuildArgDict(method, args),
            ReturnValue = returnValue,
            BearerToken = ExtractBearerToken(httpContext),
            Properties = properties,
        };
    }

    private static IDictionary<string, object?>? BuildArgDict(MethodInfo method, object?[]? args)
    {
        if (args is null)
            return null;

        var parameters = method.GetParameters();
        var dict = new Dictionary<string, object?>();
        for (var i = 0; i < parameters.Length && i < args.Length; i++)
        {
            dict[parameters[i].Name ?? $"arg{i}"] = args[i];
        }
        return dict;
    }

    private static string? ExtractBearerToken(HttpContext? httpContext)
    {
        var auth = httpContext?.Request.Headers.Authorization.FirstOrDefault();
        if (auth is not null && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return auth["Bearer ".Length..];
        }
        return null;
    }
}
