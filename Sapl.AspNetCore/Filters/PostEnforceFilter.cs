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
/// Runs a controller action carrying <see cref="PostEnforceAttribute"/>, then decides on its
/// result and replaces it with the transformed value, or denies.
/// </summary>
/// <remarks>
/// When the host registers an <see cref="ISaplTransactionManager"/>, the action and the
/// post-method enforcement run inside one transaction boundary, so any post-method failure (a
/// non-permit decision, a decision-stage obligation failure, or an output-obligation failure)
/// rolls the action's writes back. The enforcement raises <see cref="Sapl.Core.Constraints.AccessDeniedException"/>,
/// which propagates out of the boundary (rolling back) and on to the access-denied middleware for
/// the 403. Without a registered manager the <see cref="NoOpSaplTransactionManager"/> runs the
/// body directly, leaving behavior unchanged.
/// </remarks>
public sealed class PostEnforceFilter(
    EnforcementEngine engine,
    SaplSubscriptionResolver resolver,
    ISaplTransactionManager? transactionManager = null) : IAsyncActionFilter
{
    private readonly ISaplTransactionManager _transactionManager =
        transactionManager ?? NoOpSaplTransactionManager.Instance;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor ||
            descriptor.MethodInfo.GetCustomAttribute<PostEnforceAttribute>() is not { } attribute)
        {
            await next().ConfigureAwait(false);
            return;
        }

        await _transactionManager.ExecuteInTransactionAsync<object?>(async () =>
        {
            var executed = await next().ConfigureAwait(false);
            if (executed.Result is not ObjectResult { Value: { } value } result)
            {
                return null;
            }

            var subscription = resolver.Resolve(
                descriptor.MethodInfo, context.ActionArguments, SubscriptionBuilder.FromAttribute(attribute), attribute.Customizer, value);
            result.Value = await engine
                .PostEnforceAsync(subscription, value, value.GetType(), context.HttpContext.RequestAborted)
                .ConfigureAwait(false);
            return null;
        }, context.HttpContext.RequestAborted).ConfigureAwait(false);
    }
}
