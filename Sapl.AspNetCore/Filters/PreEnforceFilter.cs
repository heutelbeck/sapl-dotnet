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
/// Enforces a controller action carrying <see cref="PreEnforceAttribute"/>: decides before the
/// action runs and gates it; obligations may transform the arguments (input), the result (output),
/// or an exception (error). Denial throws and the access-denied middleware maps it to 403.
/// </summary>
public sealed class PreEnforceFilter(EnforcementEngine engine, SaplSubscriptionResolver resolver) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor ||
            descriptor.MethodInfo.GetCustomAttribute<PreEnforceAttribute>() is not { } attribute)
        {
            await next().ConfigureAwait(false);
            return;
        }

        var subscription = resolver.Resolve(
            descriptor.MethodInfo, context.ActionArguments, SubscriptionBuilder.FromAttribute(attribute), attribute.Customizer);
        var enforcement = await engine
            .PreDecideAsync(subscription, ResultType(descriptor), context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        ApplyInput(context, enforcement);

        var executed = await next().ConfigureAwait(false);
        if (executed.Exception is { } exception && !executed.ExceptionHandled)
        {
            executed.Exception = enforcement.EnforceError(exception);
        }
        else if (executed.Result is ObjectResult { Value: { } value } result)
        {
            result.Value = enforcement.EnforceOutput(value);
        }
    }

    private static void ApplyInput(ActionExecutingContext context, EnforcementContext enforcement)
    {
        var transformed = enforcement.EnforceInput(context.ActionArguments);
        if (ReferenceEquals(transformed, context.ActionArguments))
        {
            return;
        }

        context.ActionArguments.Clear();
        foreach (var (key, value) in transformed)
        {
            context.ActionArguments[key] = value;
        }
    }

    private static Type ResultType(ControllerActionDescriptor descriptor) => Unwrap(descriptor.MethodInfo.ReturnType);

    private static Type Unwrap(Type type)
    {
        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(Task<>) || definition == typeof(ValueTask<>) || definition == typeof(ActionResult<>))
            {
                return Unwrap(type.GetGenericArguments()[0]);
            }
        }

        return typeof(IActionResult).IsAssignableFrom(type) || type == typeof(Task) || type == typeof(void)
            ? typeof(object)
            : type;
    }
}
