using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Sapl.AspNetCore.Enforcement;
using Sapl.Core.Attributes;
using Sapl.Core.Authorization;
using Sapl.Core.Pep.Enforcement;
using Sapl.Core.Subscription;

namespace Sapl.AspNetCore.Filters;

/// <summary>
/// Enforces a controller action carrying <see cref="StreamEnforceAttribute"/> that returns an
/// <see cref="IAsyncEnumerable{T}"/>. The action's stream is driven through the engine's Mealy
/// machine and the enforced object stream (data items plus boundary and denial markers) becomes
/// the action result. Rendering that stream to a transport is the application's concern.
/// </summary>
public sealed class StreamEnforceFilter(EnforcementEngine engine, SaplSubscriptionResolver resolver) : IAsyncActionFilter
{
    private static readonly MethodInfo EnforceMethod =
        typeof(StreamEnforceFilter).GetMethod(nameof(Enforce), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor ||
            descriptor.MethodInfo.GetCustomAttribute<StreamEnforceAttribute>() is not { } attribute)
        {
            await next().ConfigureAwait(false);
            return;
        }

        var executed = await next().ConfigureAwait(false);
        if (executed.Result is not ObjectResult { Value: { } value } || AsyncEnumerableElementType(value.GetType()) is not { } elementType)
        {
            executed.Result = null;
            throw new InvalidOperationException(
                $"StreamEnforce requires an IAsyncEnumerable<T> return type, but {descriptor.MethodInfo.Name} returns {descriptor.MethodInfo.ReturnType.Name}.");
        }

        var subscription = resolver.Resolve(
            descriptor.MethodInfo, context.ActionArguments, SubscriptionBuilder.FromAttribute(attribute), attribute.Customizer);
        var enforced = (IAsyncEnumerable<object?>)EnforceMethod.MakeGenericMethod(elementType)
            .Invoke(this, [context.HttpContext, subscription, value, attribute.SignalTransitions, attribute.PauseRapDuringSuspend])!;
        executed.Result = new ObjectResult(enforced);
    }

    private IAsyncEnumerable<object?> Enforce<T>(
        HttpContext http, AuthorizationSubscription subscription, IAsyncEnumerable<T> source, bool signalTransitions,
        bool pauseRapDuringSuspend) =>
        engine.EnforceStreamObjectsAsync(
            subscription, Box(source, http.RequestAborted), typeof(T), signalTransitions, pauseRapDuringSuspend,
            http.RequestAborted);

    private static async IAsyncEnumerable<object?> Box<T>(
        IAsyncEnumerable<T> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    private static Type? AsyncEnumerableElementType(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
        {
            return type.GetGenericArguments()[0];
        }

        foreach (var @interface in type.GetInterfaces())
        {
            if (@interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            {
                return @interface.GetGenericArguments()[0];
            }
        }

        return null;
    }
}
