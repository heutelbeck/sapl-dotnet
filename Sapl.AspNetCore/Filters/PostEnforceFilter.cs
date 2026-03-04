using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Sapl.Core.Attributes;
using Sapl.Core.Constraints;
using Sapl.Core.Interception;
using Sapl.Core.Subscription;

namespace Sapl.AspNetCore.Filters;

public sealed class PostEnforceFilter : IAsyncResultFilter
{
    private readonly SaplMethodInterceptor _interceptor;
    private readonly ILogger<PostEnforceFilter> _logger;

    public PostEnforceFilter(SaplMethodInterceptor interceptor, ILogger<PostEnforceFilter> logger)
    {
        _interceptor = interceptor;
        _logger = logger;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var attr = GetAttribute(context);
        if (attr is null)
        {
            await next().ConfigureAwait(false);
            return;
        }

        object? returnValue = null;
        if (context.Result is ObjectResult objectResult)
        {
            returnValue = objectResult.Value;
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
            var captured = returnValue;
            var result = await _interceptor.PostEnforceAsync(
                attr, subContext, () => Task.FromResult(captured),
                context.HttpContext.RequestAborted).ConfigureAwait(false);

            if (result is JsonElement resultElement)
            {
                context.Result = new ContentResult
                {
                    Content = resultElement.GetRawText(),
                    ContentType = "application/json",
                    StatusCode = 200,
                };
            }
        }
        catch (AccessDeniedException)
        {
            _logger.LogDebug("PostEnforce denied access to {Action}.", context.ActionDescriptor.DisplayName);
            context.Result = new StatusCodeResult(403);
        }

        await next().ConfigureAwait(false);
    }

    private static PostEnforceAttribute? GetAttribute(ResultExecutingContext context)
    {
        foreach (var metadata in context.ActionDescriptor.EndpointMetadata)
        {
            if (metadata is PostEnforceAttribute attr)
                return attr;
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
