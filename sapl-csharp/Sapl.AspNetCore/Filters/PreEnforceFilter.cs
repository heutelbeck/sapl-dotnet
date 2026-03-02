using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Sapl.AspNetCore.Attributes;
using Sapl.AspNetCore.Subscription;
using Sapl.Core.Constraints;
using Sapl.Core.Enforcement;

namespace Sapl.AspNetCore.Filters;

public sealed class PreEnforceFilter : IAsyncActionFilter
{
    private readonly EnforcementEngine _engine;
    private readonly ILogger<PreEnforceFilter> _logger;

    public PreEnforceFilter(EnforcementEngine engine, ILogger<PreEnforceFilter> logger)
    {
        _engine = engine;
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
            HttpContext = context.HttpContext,
            MethodName = context.ActionDescriptor.DisplayName,
            ClassName = context.Controller?.GetType().Name,
            MethodArguments = context.ActionArguments,
        };

        var builder = SubscriptionBuilder.FromAttribute(attr);
        var subscription = builder.Build(subContext);

        var result = await _engine.PreEnforceAsync(subscription, context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        if (!result.IsPermitted)
        {
            _logger.LogDebug("PreEnforce denied access to {Action}.", context.ActionDescriptor.DisplayName);
            context.Result = new StatusCodeResult(403);
            return;
        }

        if (result.Bundle is not null)
        {
            if (result.Bundle.HasResourceReplacement)
            {
                _logger.LogDebug("PreEnforce: returning PDP-provided resource replacement.");
                context.Result = new ContentResult
                {
                    Content = result.Bundle.ResourceReplacement!.Value.GetRawText(),
                    ContentType = "application/json",
                    StatusCode = 200,
                };
                return;
            }

            var miContext = new Core.Constraints.Api.MethodInvocationContext(
                context.ActionArguments.Values.ToArray(),
                subContext.MethodName ?? "unknown",
                subContext.ClassName,
                context.HttpContext.Request);
            result.Bundle.HandleMethodInvocationHandlers(miContext);

            for (var i = 0; i < miContext.Args.Length; i++)
            {
                var keys = context.ActionArguments.Keys.ToArray();
                if (i < keys.Length)
                {
                    context.ActionArguments[keys[i]] = miContext.Args[i];
                }
            }
        }

        var executedContext = await next().ConfigureAwait(false);

        if (executedContext.Exception is not null && result.Bundle is not null)
        {
            var transformed = result.Bundle.HandleAllOnErrorConstraints(executedContext.Exception);
            executedContext.ExceptionHandled = true;
            context.HttpContext.Response.StatusCode = 500;
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsync(
                System.Text.Json.JsonSerializer.Serialize(new { error = transformed.Message }));
            return;
        }

        if (executedContext.Result is ObjectResult objectResult && objectResult.Value is not null
            && result.Bundle is not null)
        {
            try
            {
                var transformed = result.Bundle.HandleAllOnNextConstraints(objectResult.Value);
                result.Bundle.CheckFailedObligations();

                if (transformed is System.Text.Json.JsonElement jsonElement)
                {
                    executedContext.Result = new ContentResult
                    {
                        Content = jsonElement.GetRawText(),
                        ContentType = "application/json",
                        StatusCode = objectResult.StatusCode ?? 200,
                    };
                }
                else
                {
                    objectResult.Value = transformed;
                }
            }
            catch (AccessDeniedException ex)
            {
                _logger.LogDebug(ex, "PreEnforce denied due to post-execution constraint handling.");
                executedContext.Result = new StatusCodeResult(403);
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
}
