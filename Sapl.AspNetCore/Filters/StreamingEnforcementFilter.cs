using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Sapl.AspNetCore.Streaming;
using Sapl.Core.Attributes;
using Sapl.Core.Constraints;
using Sapl.Core.Interception;
using Sapl.Core.Subscription;

namespace Sapl.AspNetCore.Filters;

public sealed class StreamingEnforcementFilter : IAsyncResultFilter
{
    private readonly SaplMethodInterceptor _interceptor;
    private readonly ILogger<StreamingEnforcementFilter> _logger;

    public StreamingEnforcementFilter(SaplMethodInterceptor interceptor, ILogger<StreamingEnforcementFilter> logger)
    {
        _interceptor = interceptor;
        _logger = logger;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var (attr, elementType) = GetStreamingAttribute(context);
        if (attr is null || elementType is null)
        {
            await next().ConfigureAwait(false);
            return;
        }

        if (context.Result is not ObjectResult objectResult || objectResult.Value is null)
        {
            await next().ConfigureAwait(false);
            return;
        }

        var subContext = new SubscriptionContext
        {
            Principal = context.HttpContext.User,
            MethodName = context.ActionDescriptor.DisplayName,
            ClassName = context.Controller?.GetType().Name,
            BearerToken = ExtractBearerToken(context.HttpContext),
            Properties = BuildProperties(context.HttpContext),
        };

        try
        {
            var method = typeof(StreamingEnforcementFilter)
                .GetMethod(nameof(WrapAndWriteStream), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(elementType);

            var task = (Task?)method.Invoke(this, [attr, subContext, objectResult.Value, context, context.HttpContext.RequestAborted]);
            if (task is not null)
                await task.ConfigureAwait(false);
        }
        catch (AccessDeniedException)
        {
            _logger.LogDebug("Streaming enforcement denied access to {Action}.", context.ActionDescriptor.DisplayName);
            context.Result = new StatusCodeResult(403);
            await next().ConfigureAwait(false);
        }
    }

    private async Task WrapAndWriteStream<T>(
        Attribute attr,
        SubscriptionContext context,
        object source,
        ResultExecutingContext resultContext,
        CancellationToken ct)
    {
        var asyncEnumerable = (IAsyncEnumerable<T>)source;
        IAsyncEnumerable<T> SourceFactory() => asyncEnumerable;

        var wrappedStream = attr switch
        {
            EnforceTillDeniedAttribute tillDenied =>
                _interceptor.EnforceTillDenied<T>(tillDenied, context, SourceFactory, ct),
            EnforceDropWhileDeniedAttribute dropWhileDenied =>
                _interceptor.EnforceDropWhileDenied<T>(dropWhileDenied, context, SourceFactory, ct),
            EnforceRecoverableIfDeniedAttribute recoverableIfDenied =>
                _interceptor.EnforceRecoverableIfDenied<T>(recoverableIfDenied, context, SourceFactory, ct),
            _ => throw new InvalidOperationException($"Unknown streaming enforcement attribute: {attr.GetType().Name}"),
        };

        await SseResultAdapter.WriteSseStreamAsync(resultContext.HttpContext, wrappedStream, ct)
            .ConfigureAwait(false);
    }

    private static (Attribute? attr, Type? elementType) GetStreamingAttribute(ResultExecutingContext context)
    {
        foreach (var metadata in context.ActionDescriptor.EndpointMetadata)
        {
            switch (metadata)
            {
                case EnforceTillDeniedAttribute tillDenied:
                    return (tillDenied, GetAsyncEnumerableElementType(context));
                case EnforceDropWhileDeniedAttribute dropWhileDenied:
                    return (dropWhileDenied, GetAsyncEnumerableElementType(context));
                case EnforceRecoverableIfDeniedAttribute recoverableIfDenied:
                    return (recoverableIfDenied, GetAsyncEnumerableElementType(context));
            }
        }
        return (null, null);
    }

    private static Type? GetAsyncEnumerableElementType(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult objectResult || objectResult.Value is null)
            return null;

        var valueType = objectResult.Value.GetType();
        foreach (var iface in valueType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            {
                return iface.GetGenericArguments()[0];
            }
        }
        return null;
    }

    private static string? ExtractBearerToken(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        var auth = httpContext.Request.Headers.Authorization.FirstOrDefault();
        if (auth is not null && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return auth["Bearer ".Length..];
        }
        return null;
    }

    private static Dictionary<string, object?> BuildProperties(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        var properties = new Dictionary<string, object?>
        {
            ["path"] = httpContext.Request.Path.Value,
            ["httpMethod"] = httpContext.Request.Method,
        };

        var routeValues = httpContext.Request.RouteValues;
        if (routeValues.Count > 0)
        {
            properties["params"] = routeValues.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString());
        }

        var query = httpContext.Request.Query;
        if (query.Count > 0)
        {
            properties["query"] = query.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
        }

        return properties;
    }
}
