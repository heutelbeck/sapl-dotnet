using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Sapl.AspNetCore.Enforcement;
using Sapl.Core.Attributes;
using Sapl.Core.Pep.Enforcement;
using Sapl.Core.Subscription;

namespace Sapl.AspNetCore.Filters;

/// <summary>
/// Runs a controller action carrying <see cref="PostEnforceAttribute"/>, then decides on its
/// result and replaces it with the transformed value, or denies.
/// </summary>
public sealed class PostEnforceFilter(EnforcementEngine engine, SaplSubscriptionResolver resolver) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor ||
            descriptor.MethodInfo.GetCustomAttribute<PostEnforceAttribute>() is not { } attribute)
        {
            await next().ConfigureAwait(false);
            return;
        }

        var executed = await next().ConfigureAwait(false);
        if (executed.Result is not ObjectResult { Value: { } value } result)
        {
            return;
        }

        var subscription = resolver.Resolve(
            descriptor.MethodInfo, context.ActionArguments, SubscriptionBuilder.FromAttribute(attribute), attribute.Customizer, value);
        result.Value = await engine
            .PostEnforceAsync(subscription, value, value.GetType(), context.HttpContext.RequestAborted)
            .ConfigureAwait(false);
    }
}
