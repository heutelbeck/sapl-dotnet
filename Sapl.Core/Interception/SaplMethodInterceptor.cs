using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sapl.Core.Attributes;
using Sapl.Core.Authorization;
using Sapl.Core.Constraints;
using Sapl.Core.Constraints.Api;
using Sapl.Core.Enforcement;
using Sapl.Core.Subscription;

namespace Sapl.Core.Interception;

public sealed class SaplMethodInterceptor
{
    private readonly EnforcementEngine _engine;
    private readonly ILogger<SaplMethodInterceptor> _logger;
    private readonly IServiceProvider _serviceProvider;

    public SaplMethodInterceptor(
        EnforcementEngine engine,
        ILogger<SaplMethodInterceptor> logger,
        IServiceProvider serviceProvider)
    {
        _engine = engine;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<object?> PreEnforceAsync(
        PreEnforceAttribute attribute,
        SubscriptionContext context,
        Func<object?[], Task<object?>> proceed,
        object?[] args,
        CancellationToken ct = default)
    {
        var builder = SubscriptionBuilder.FromAttribute(attribute);
        ApplyCustomizer(attribute.Customizer, context, builder);
        var subscription = builder.Build(context);

        var result = await _engine.PreEnforceAsync(subscription, ct).ConfigureAwait(false);

        if (!result.IsPermitted)
        {
            _logger.LogDebug("PreEnforce denied access to {Method}.", context.MethodName);
            throw new AccessDeniedException("Access denied by policy.");
        }

        if (result.Bundle is not null)
        {
            if (result.Bundle.HasResourceReplacement)
            {
                _logger.LogDebug("PreEnforce: returning PDP-provided resource replacement.");
                return result.Bundle.ResourceReplacement!.Value;
            }

            var miContext = new MethodInvocationContext(
                args,
                context.MethodName ?? "unknown",
                context.ClassName,
                context.Properties.TryGetValue("request", out var request) ? request : null);
            result.Bundle.HandleMethodInvocationHandlers(miContext);

            for (var i = 0; i < miContext.Args.Length && i < args.Length; i++)
            {
                args[i] = miContext.Args[i];
            }
        }

        object? returnValue;
        try
        {
            returnValue = await proceed(args).ConfigureAwait(false);
        }
        catch (Exception ex) when (result.Bundle is not null)
        {
            var transformed = result.Bundle.HandleAllOnErrorConstraints(ex);
            throw new InvalidOperationException(transformed.Message, transformed);
        }

        if (returnValue is not null && result.Bundle is not null)
        {
            try
            {
                returnValue = result.Bundle.HandleAllOnNextConstraints(returnValue);
                result.Bundle.CheckFailedObligations();
            }
            catch (AccessDeniedException)
            {
                _logger.LogDebug("PreEnforce denied due to post-execution constraint handling.");
                throw;
            }
        }

        return returnValue;
    }

    public async Task<object?> PostEnforceAsync(
        PostEnforceAttribute attribute,
        SubscriptionContext context,
        Func<Task<object?>> proceed,
        CancellationToken ct = default)
    {
        var returnValue = await proceed().ConfigureAwait(false);

        var contextWithReturnValue = new SubscriptionContext
        {
            Principal = context.Principal,
            MethodName = context.MethodName,
            ClassName = context.ClassName,
            MethodArguments = context.MethodArguments,
            ReturnValue = returnValue,
            BearerToken = context.BearerToken,
            Properties = context.Properties,
        };

        var builder = SubscriptionBuilder.FromAttribute(attribute);
        ApplyCustomizer(attribute.Customizer, contextWithReturnValue, builder);
        var subscription = builder.Build(contextWithReturnValue);

        var element = returnValue is not null
            ? JsonSerializer.SerializeToElement(returnValue, SerializerDefaults.Options)
            : JsonDocument.Parse("null").RootElement;

        var result = await _engine.PostEnforceAsync(subscription, element, ct).ConfigureAwait(false);

        if (!result.IsPermitted)
        {
            _logger.LogDebug("PostEnforce denied access to {Method}.", context.MethodName);
            throw new AccessDeniedException("Access denied by policy.");
        }

        return result.Value;
    }

    public IAsyncEnumerable<T> EnforceTillDenied<T>(
        EnforceTillDeniedAttribute attribute,
        SubscriptionContext context,
        Func<IAsyncEnumerable<T>> sourceFactory,
        CancellationToken ct = default)
    {
        var builder = SubscriptionBuilder.FromAttribute(attribute);
        ApplyCustomizer(attribute.Customizer, context, builder);
        var subscription = builder.Build(context);
        return _engine.EnforceTillDenied(subscription, sourceFactory, cancellationToken: ct);
    }

    public IAsyncEnumerable<T> EnforceDropWhileDenied<T>(
        EnforceDropWhileDeniedAttribute attribute,
        SubscriptionContext context,
        Func<IAsyncEnumerable<T>> sourceFactory,
        CancellationToken ct = default)
    {
        var builder = SubscriptionBuilder.FromAttribute(attribute);
        ApplyCustomizer(attribute.Customizer, context, builder);
        var subscription = builder.Build(context);
        return _engine.EnforceDropWhileDenied(subscription, sourceFactory, cancellationToken: ct);
    }

    public IAsyncEnumerable<T> EnforceRecoverableIfDenied<T>(
        EnforceRecoverableIfDeniedAttribute attribute,
        SubscriptionContext context,
        Func<IAsyncEnumerable<T>> sourceFactory,
        CancellationToken ct = default)
    {
        var builder = SubscriptionBuilder.FromAttribute(attribute);
        ApplyCustomizer(attribute.Customizer, context, builder);
        var subscription = builder.Build(context);

        if (typeof(T) != typeof(object))
        {
            return _engine.EnforceRecoverableIfDenied(subscription, sourceFactory, cancellationToken: ct);
        }

        return (IAsyncEnumerable<T>)EnforceRecoverableWithSignals(
            subscription, (Func<IAsyncEnumerable<object>>)(object)sourceFactory, ct);
    }

    private void ApplyCustomizer(Type? customizerType, SubscriptionContext context, SubscriptionBuilder builder)
    {
        if (customizerType is null)
            return;

        if (!typeof(ISubscriptionCustomizer).IsAssignableFrom(customizerType))
            throw new InvalidOperationException(
                $"Customizer type {customizerType.Name} does not implement ISubscriptionCustomizer.");

        var customizer = (ISubscriptionCustomizer)ActivatorUtilities.GetServiceOrCreateInstance(
            _serviceProvider, customizerType);
        customizer.Customize(context, builder);
    }

    private async IAsyncEnumerable<object> EnforceRecoverableWithSignals(
        Authorization.AuthorizationSubscription subscription,
        Func<IAsyncEnumerable<object>> sourceFactory,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var output = System.Threading.Channels.Channel.CreateUnbounded<object>();

        var stream = _engine.EnforceRecoverableIfDenied(
            subscription, sourceFactory,
            onDeny: _ => output.Writer.TryWrite(AccessSignal.Denied()),
            onRecover: _ => output.Writer.TryWrite(AccessSignal.Recovered()),
            cancellationToken: ct);

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in stream.WithCancellation(ct).ConfigureAwait(false))
                {
                    await output.Writer.WriteAsync(item, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                output.Writer.TryComplete();
            }
        }, ct);

        await foreach (var item in output.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return item;
        }
    }
}
