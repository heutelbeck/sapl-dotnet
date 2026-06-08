using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Sapl.AspNetCore.Enforcement;
using Sapl.AspNetCore.Streaming;
using Sapl.Core.Attributes;
using Sapl.Core.Authorization;
using Sapl.Core.Constraints;
using Sapl.Core.Pep.Enforcement;
using Sapl.Core.Pep.Streaming;
using Sapl.Core.Subscription;

namespace Sapl.AspNetCore.Filters;

/// <summary>
/// Enforces a controller action carrying <see cref="StreamEnforceAttribute"/> that returns an
/// <see cref="IAsyncEnumerable{T}"/>. The result stream is driven through the engine's Mealy
/// machine and rendered as Server-Sent Events. A denial closes the stream; since SSE headers are
/// sent before the first item, it cannot become a 403.
/// </summary>
public sealed class StreamEnforceFilter(EnforcementEngine engine, SaplSubscriptionResolver resolver) : IAsyncActionFilter
{
    private static readonly MethodInfo RenderMethod =
        typeof(StreamEnforceFilter).GetMethod(nameof(RenderAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

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
            return;
        }

        var subscription = resolver.Resolve(
            descriptor.MethodInfo, context.ActionArguments, SubscriptionBuilder.FromAttribute(attribute), attribute.Customizer);
        var render = (Task)RenderMethod.MakeGenericMethod(elementType)
            .Invoke(this, [context.HttpContext, subscription, value, attribute.SignalTransitions])!;
        await render.ConfigureAwait(false);
        executed.Result = new EmptyResult();
    }

    private async Task RenderAsync<T>(
        HttpContext http, AuthorizationSubscription subscription, IAsyncEnumerable<T> source, bool signalTransitions)
    {
        var enforced = engine.EnforceStreamObjectsAsync(
            subscription, Box(source, http.RequestAborted), typeof(T), signalTransitions, http.RequestAborted);
        await SseResultAdapter.WriteSseStreamAsync(http, MapFramesAsync(enforced, http.RequestAborted), http.RequestAborted)
            .ConfigureAwait(false);
    }

    private static async IAsyncEnumerable<object?> Box<T>(
        IAsyncEnumerable<T> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    private static async IAsyncEnumerable<object?> MapFramesAsync(
        IAsyncEnumerable<object?> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return item switch
            {
                TransitionReason.Suspended => new StreamSignalFrame("ACCESS_SUSPENDED", "Stream paused by policy"),
                TransitionReason.Granted => new StreamSignalFrame("ACCESS_GRANTED", "Access granted by policy"),
                AccessDeniedException => new StreamSignalFrame("ACCESS_DENIED", "Stream terminated by policy"),
                _ => item,
            };
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
