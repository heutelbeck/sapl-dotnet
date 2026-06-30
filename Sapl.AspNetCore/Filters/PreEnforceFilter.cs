using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Sapl.AspNetCore.Enforcement;
using Sapl.Core.Attributes;
using Sapl.Core.Pep.Enforcement;
using Sapl.Core.Pep.Transactions;
using Sapl.Core.Subscription;

namespace Sapl.AspNetCore.Filters;

/// <summary>
/// Enforces a controller action carrying <see cref="PreEnforceAttribute"/>: decides before the
/// action runs and gates it; obligations may transform the arguments (input), the result (output),
/// or an exception (error). Denial throws and the access-denied middleware maps it to 403.
/// </summary>
/// <remarks>
/// When the host registers an <see cref="ISaplTransactionManager"/>, the action and the
/// output-obligation enforcement run inside one transaction boundary, so an output-obligation
/// failure after the action has written rolls the write back. The decision gate runs before the
/// boundary, so a denial there means the action never ran and there is nothing to roll back.
/// Without a registered manager the <see cref="NoOpSaplTransactionManager"/> runs the body
/// directly, leaving behavior unchanged.
/// </remarks>
public sealed class PreEnforceFilter(
    EnforcementEngine engine,
    SaplSubscriptionResolver resolver,
    ISaplTransactionManager? transactionManager = null) : IAsyncActionFilter
{
    private readonly ISaplTransactionManager _transactionManager =
        transactionManager ?? NoOpSaplTransactionManager.Instance;

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

        await _transactionManager.ExecuteInTransactionAsync<object?>(async () =>
        {
            var executed = await next().ConfigureAwait(false);
            if (executed.Exception is { } exception && !executed.ExceptionHandled)
            {
                executed.Exception = enforcement.EnforceError(exception);
            }
            else
            {
                // Output obligations run for every result shape, including a null payload or a
                // result that carries no payload at all. An output-obligation failure throws
                // AccessDeniedException out of the boundary, rolling back the action's writes; the
                // exception then propagates to the access-denied middleware, which maps it to 403.
                var payload = executed.Result is ObjectResult original ? original.Value : null;
                var enforced = enforcement.EnforceOutput(payload);
                if (executed.Result is ObjectResult result)
                {
                    result.Value = enforced;
                }
                else if (!ReferenceEquals(enforced, payload))
                {
                    executed.Result = new ObjectResult(enforced);
                }
            }

            return null;
        }, context.HttpContext.RequestAborted).ConfigureAwait(false);
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
