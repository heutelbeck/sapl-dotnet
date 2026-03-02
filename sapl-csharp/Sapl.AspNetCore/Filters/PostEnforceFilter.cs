using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Sapl.AspNetCore.Attributes;
using Sapl.AspNetCore.Subscription;
using Sapl.Core.Authorization;
using Sapl.Core.Enforcement;

namespace Sapl.AspNetCore.Filters;

public sealed class PostEnforceFilter : IAsyncResultFilter
{
    private readonly EnforcementEngine _engine;
    private readonly ILogger<PostEnforceFilter> _logger;

    public PostEnforceFilter(EnforcementEngine engine, ILogger<PostEnforceFilter> logger)
    {
        _engine = engine;
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
            HttpContext = context.HttpContext,
            MethodName = context.ActionDescriptor.DisplayName,
            ClassName = context.Controller?.GetType().Name,
            ReturnValue = returnValue,
        };

        var builder = SubscriptionBuilder.FromAttribute(attr);

        var subscription = builder.Build(subContext);

        var element = returnValue is not null
            ? JsonSerializer.SerializeToElement(returnValue, SerializerDefaults.Options)
            : JsonDocument.Parse("null").RootElement;

        var result = await _engine.PostEnforceAsync(
            subscription,
            element,
            context.HttpContext.RequestAborted).ConfigureAwait(false);

        if (!result.IsPermitted)
        {
            _logger.LogDebug("PostEnforce denied access to {Action}.", context.ActionDescriptor.DisplayName);
            context.Result = new StatusCodeResult(403);
            await next().ConfigureAwait(false);
            return;
        }

        if (result.Value is JsonElement resultElement)
        {
            context.Result = new ContentResult
            {
                Content = resultElement.GetRawText(),
                ContentType = "application/json",
                StatusCode = 200,
            };
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
}
