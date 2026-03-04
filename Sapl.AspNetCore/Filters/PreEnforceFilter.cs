using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Sapl.Core.Attributes;
using Sapl.Core.Constraints;
using Sapl.Core.Interception;
using Sapl.Core.Subscription;

namespace Sapl.AspNetCore.Filters;

public sealed class PreEnforceFilter : IAsyncActionFilter
{
    private readonly SaplMethodInterceptor _interceptor;
    private readonly ILogger<PreEnforceFilter> _logger;

    public PreEnforceFilter(SaplMethodInterceptor interceptor, ILogger<PreEnforceFilter> logger)
    {
        _interceptor = interceptor;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var attr = GetAttribute(context);
        if (attr is null)
        {
            await next().ConfigureAwait(false);
            return;
        }

        var subContext = new SubscriptionContext
        {
            Principal = context.HttpContext.User,
            MethodName = context.ActionDescriptor.DisplayName,
            ClassName = context.Controller?.GetType().Name,
            MethodArguments = context.ActionArguments,
            BearerToken = ExtractBearerToken(context.HttpContext),
            Properties = BuildProperties(context.HttpContext),
        };

        var args = context.ActionArguments.Values.ToArray();
        ActionExecutedContext? executedCtx = null;

        try
        {
            var result = await _interceptor.PreEnforceAsync(
                attr, subContext, async updatedArgs =>
                {
                    for (var i = 0; i < updatedArgs.Length; i++)
                    {
                        var keys = context.ActionArguments.Keys.ToArray();
                        if (i < keys.Length)
                        {
                            context.ActionArguments[keys[i]] = updatedArgs[i];
                        }
                    }

                    executedCtx = await next().ConfigureAwait(false);

                    if (executedCtx.Exception is not null)
                    {
                        throw executedCtx.Exception;
                    }

                    return (executedCtx.Result as ObjectResult)?.Value;
                }, args, context.HttpContext.RequestAborted).ConfigureAwait(false);

            if (executedCtx is not null)
            {
                if (result is JsonElement jsonElement)
                {
                    executedCtx.Result = new ContentResult
                    {
                        Content = jsonElement.GetRawText(),
                        ContentType = "application/json",
                        StatusCode = 200,
                    };
                }
                else if (result is not null)
                {
                    executedCtx.Result = new ObjectResult(result);
                }
            }
        }
        catch (AccessDeniedException)
        {
            _logger.LogDebug("PreEnforce denied access to {Action}.", context.ActionDescriptor.DisplayName);
            if (executedCtx is not null)
            {
                executedCtx.Result = new StatusCodeResult(403);
            }
            else
            {
                context.Result = new StatusCodeResult(403);
            }
        }
    }

    private static PreEnforceAttribute? GetAttribute(ActionExecutingContext context)
    {
        foreach (var metadata in context.ActionDescriptor.EndpointMetadata)
        {
            if (metadata is PreEnforceAttribute attr)
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
            ["request"] = httpContext.Request,
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
