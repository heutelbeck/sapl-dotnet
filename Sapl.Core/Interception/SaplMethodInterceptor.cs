using Microsoft.Extensions.DependencyInjection;
using Sapl.Core.Attributes;
using Sapl.Core.Authorization;
using Sapl.Core.Pep.Enforcement;
using Sapl.Core.Pep.Transactions;
using Sapl.Core.Subscription;

namespace Sapl.Core.Interception;

/// <summary>
/// Applies SAPL enforcement at the domain layer. The proxy invokes one of these methods per
/// intercepted service call, building a subscription from the method context (and an optional
/// customizer) and routing through the enforcement engine, so policies can enforce on service
/// methods, not only at the HTTP boundary.
/// </summary>
/// <remarks>
/// When the host registers an <see cref="ISaplTransactionManager"/>, the protected invocation and
/// the enforcement that depends on its result run inside one transaction boundary, so a denial
/// after a write rolls the write back. Without a registered manager the
/// <see cref="NoOpSaplTransactionManager"/> runs the body directly, leaving behavior unchanged.
/// </remarks>
public sealed class SaplMethodInterceptor
{
    private readonly EnforcementEngine _engine;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISaplTransactionManager _transactionManager;

    public SaplMethodInterceptor(
        EnforcementEngine engine,
        IServiceProvider serviceProvider,
        ISaplTransactionManager? transactionManager = null)
    {
        _engine = engine;
        _serviceProvider = serviceProvider;
        _transactionManager = transactionManager ?? NoOpSaplTransactionManager.Instance;
    }

    public Task<object?> PreEnforceAsync(
        PreEnforceAttribute attribute,
        SubscriptionContext context,
        Type returnType,
        IReadOnlyList<string> parameterNames,
        object?[] args,
        Func<object?[], Task<object?>> proceed,
        CancellationToken cancellationToken = default)
    {
        var subscription = Build(SubscriptionBuilder.FromAttribute(attribute), attribute.Customizer, context);
        return _transactionManager.ExecuteInTransactionAsync(async () =>
        {
            var enforcement = await _engine.PreDecideAsync(subscription, returnType, cancellationToken).ConfigureAwait(false);

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
        }, cancellationToken);
    }

    public Task<object?> PostEnforceAsync(
        PostEnforceAttribute attribute,
        SubscriptionContext context,
        Type returnType,
        Func<Task<object?>> proceed,
        CancellationToken cancellationToken = default) =>
        _transactionManager.ExecuteInTransactionAsync(async () =>
        {
            var returnValue = await proceed().ConfigureAwait(false);
            var withResult = WithReturnValue(context, returnValue);
            var subscription = Build(SubscriptionBuilder.FromAttribute(attribute), attribute.Customizer, withResult);
            return await _engine.PostEnforceAsync(subscription, returnValue, returnType, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    public IAsyncEnumerable<T> EnforceStream<T>(
        StreamEnforceAttribute attribute,
        SubscriptionContext context,
        Func<IAsyncEnumerable<T>> sourceFactory,
        CancellationToken cancellationToken = default)
    {
        var subscription = Build(SubscriptionBuilder.FromAttribute(attribute), attribute.Customizer, context);
        return _engine.EnforceStreamAsync(subscription, sourceFactory(), attribute.PauseRapDuringSuspend, cancellationToken);
    }

    public IAsyncEnumerable<object?> EnforceStreamObjects(
        StreamEnforceAttribute attribute,
        SubscriptionContext context,
        Func<IAsyncEnumerable<object?>> sourceFactory,
        CancellationToken cancellationToken = default)
    {
        var subscription = Build(SubscriptionBuilder.FromAttribute(attribute), attribute.Customizer, context);
        return _engine.EnforceStreamObjectsAsync(
            subscription, sourceFactory(), typeof(object), attribute.SignalTransitions, attribute.PauseRapDuringSuspend,
            cancellationToken);
    }

    private AuthorizationSubscription Build(SubscriptionBuilder builder, Type? customizerType, SubscriptionContext context)
    {
        if (customizerType is not null)
        {
            var customizer = (ISubscriptionCustomizer)ActivatorUtilities.GetServiceOrCreateInstance(_serviceProvider, customizerType);
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
