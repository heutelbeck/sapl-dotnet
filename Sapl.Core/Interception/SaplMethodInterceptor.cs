using Microsoft.Extensions.DependencyInjection;
using Sapl.Core.Attributes;
using Sapl.Core.Authorization;
using Sapl.Core.Pep.Enforcement;
using Sapl.Core.Subscription;

namespace Sapl.Core.Interception;

/// <summary>
/// Applies SAPL enforcement at the domain layer. The proxy invokes one of these methods per
/// intercepted service call, building a subscription from the method context (and an optional
/// customizer) and routing through the enforcement engine, so policies can enforce on service
/// methods, not only at the HTTP boundary.
/// </summary>
public sealed class SaplMethodInterceptor(EnforcementEngine engine, IServiceProvider serviceProvider)
{
    public async Task<object?> PreEnforceAsync(
        PreEnforceAttribute attribute,
        SubscriptionContext context,
        Type returnType,
        IReadOnlyList<string> parameterNames,
        object?[] args,
        Func<object?[], Task<object?>> proceed,
        CancellationToken cancellationToken = default)
    {
        var subscription = Build(SubscriptionBuilder.FromAttribute(attribute), attribute.Customizer, context);
        var enforcement = await engine.PreDecideAsync(subscription, returnType, cancellationToken).ConfigureAwait(false);

        ApplyInput(enforcement, context, parameterNames, args);

        object? returnValue;
        try
        {
            returnValue = await proceed(args).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw enforcement.EnforceError(exception);
        }

        return returnValue is null ? null : enforcement.EnforceOutput(returnValue);
    }

    public async Task<object?> PostEnforceAsync(
        PostEnforceAttribute attribute,
        SubscriptionContext context,
        Type returnType,
        Func<Task<object?>> proceed,
        CancellationToken cancellationToken = default)
    {
        var returnValue = await proceed().ConfigureAwait(false);
        var withResult = WithReturnValue(context, returnValue);
        var subscription = Build(SubscriptionBuilder.FromAttribute(attribute), attribute.Customizer, withResult);
        return await engine.PostEnforceAsync(subscription, returnValue, returnType, cancellationToken).ConfigureAwait(false);
    }

    public IAsyncEnumerable<T> EnforceStream<T>(
        StreamEnforceAttribute attribute,
        SubscriptionContext context,
        Func<IAsyncEnumerable<T>> sourceFactory,
        CancellationToken cancellationToken = default)
    {
        var subscription = Build(SubscriptionBuilder.FromAttribute(attribute), attribute.Customizer, context);
        return engine.EnforceStreamAsync(subscription, sourceFactory(), cancellationToken);
    }

    private AuthorizationSubscription Build(SubscriptionBuilder builder, Type? customizerType, SubscriptionContext context)
    {
        if (customizerType is not null)
        {
            var customizer = (ISubscriptionCustomizer)ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, customizerType);
            customizer.Customize(context, builder);
        }

        return builder.Build(context);
    }

    private static void ApplyInput(
        EnforcementContext enforcement,
        SubscriptionContext context,
        IReadOnlyList<string> parameterNames,
        object?[] args)
    {
        if (context.MethodArguments is null)
        {
            return;
        }

        var transformed = enforcement.EnforceInput(context.MethodArguments);
        for (var i = 0; i < parameterNames.Count && i < args.Length; i++)
        {
            if (transformed.TryGetValue(parameterNames[i], out var value))
            {
                args[i] = value;
            }
        }
    }

    private static SubscriptionContext WithReturnValue(SubscriptionContext context, object? returnValue) => new()
    {
        Principal = context.Principal,
        MethodName = context.MethodName,
        ClassName = context.ClassName,
        MethodArguments = context.MethodArguments,
        ReturnValue = returnValue,
        BearerToken = context.BearerToken,
        Properties = context.Properties,
    };
}
