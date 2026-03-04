using System.Reflection;
using Sapl.Core.Attributes;
using Sapl.Core.Constraints;
using Sapl.Core.Interception;

namespace Sapl.AspNetCore.Interception;

public class SaplProxy<T> : DispatchProxy where T : class
{
    internal T Target { get; set; } = null!;
    internal SaplMethodInterceptor Interceptor { get; set; } = null!;
    internal HttpSubscriptionContextFactory ContextFactory { get; set; } = null!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null)
            return null;

        var preAttr = targetMethod.GetCustomAttribute<PreEnforceAttribute>();
        var postAttr = targetMethod.GetCustomAttribute<PostEnforceAttribute>();
        var tillDenied = targetMethod.GetCustomAttribute<EnforceTillDeniedAttribute>();
        var dropWhileDenied = targetMethod.GetCustomAttribute<EnforceDropWhileDeniedAttribute>();
        var recoverableIfDenied = targetMethod.GetCustomAttribute<EnforceRecoverableIfDeniedAttribute>();

        if (preAttr is null && postAttr is null && tillDenied is null
            && dropWhileDenied is null && recoverableIfDenied is null)
        {
            return targetMethod.Invoke(Target, args);
        }

        var context = ContextFactory.Create(targetMethod, args);
        var effectiveArgs = args ?? [];

        if (preAttr is not null)
            return DispatchPreEnforce(targetMethod, effectiveArgs, context, preAttr);
        if (postAttr is not null)
            return DispatchPostEnforce(targetMethod, effectiveArgs, context, postAttr);
        if (tillDenied is not null)
            return DispatchStreaming(targetMethod, effectiveArgs, context, tillDenied);
        if (dropWhileDenied is not null)
            return DispatchStreaming(targetMethod, effectiveArgs, context, dropWhileDenied);
        if (recoverableIfDenied is not null)
            return DispatchStreaming(targetMethod, effectiveArgs, context, recoverableIfDenied);

        return targetMethod.Invoke(Target, args);
    }

    private object? DispatchPreEnforce(
        MethodInfo targetMethod, object?[] args,
        Core.Subscription.SubscriptionContext context,
        PreEnforceAttribute attr)
    {
        var returnType = targetMethod.ReturnType;

        if (returnType == typeof(Task))
        {
            return PreEnforceVoidAsync(targetMethod, args, context, attr);
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            var method = typeof(SaplProxy<T>)
                .GetMethod(nameof(PreEnforceTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(resultType);
            return method.Invoke(this, [targetMethod, args, context, attr]);
        }

        return PreEnforceSyncFallback(targetMethod, args, context, attr);
    }

    private async Task PreEnforceVoidAsync(
        MethodInfo targetMethod, object?[] args,
        Core.Subscription.SubscriptionContext context,
        PreEnforceAttribute attr)
    {
        await Interceptor.PreEnforceAsync(attr, context, async updatedArgs =>
        {
            var task = (Task?)targetMethod.Invoke(Target, updatedArgs);
            if (task is not null)
                await task.ConfigureAwait(false);
            return null;
        }, args).ConfigureAwait(false);
    }

    private async Task<TResult?> PreEnforceTypedAsync<TResult>(
        MethodInfo targetMethod, object?[] args,
        Core.Subscription.SubscriptionContext context,
        PreEnforceAttribute attr)
    {
        var result = await Interceptor.PreEnforceAsync(attr, context, async updatedArgs =>
        {
            var task = (Task<TResult>?)targetMethod.Invoke(Target, updatedArgs);
            return task is not null ? await task.ConfigureAwait(false) : default;
        }, args).ConfigureAwait(false);

        return result is TResult typed ? typed : default;
    }

    private object? PreEnforceSyncFallback(
        MethodInfo targetMethod, object?[] args,
        Core.Subscription.SubscriptionContext context,
        PreEnforceAttribute attr)
    {
        return Interceptor.PreEnforceAsync(attr, context, updatedArgs =>
        {
            var result = targetMethod.Invoke(Target, updatedArgs);
            return Task.FromResult(result);
        }, args).GetAwaiter().GetResult();
    }

    private object? DispatchPostEnforce(
        MethodInfo targetMethod, object?[] args,
        Core.Subscription.SubscriptionContext context,
        PostEnforceAttribute attr)
    {
        var returnType = targetMethod.ReturnType;

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            var method = typeof(SaplProxy<T>)
                .GetMethod(nameof(PostEnforceTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(resultType);
            return method.Invoke(this, [targetMethod, args, context, attr]);
        }

        return PostEnforceSyncFallback(targetMethod, args, context, attr);
    }

    private async Task<TResult?> PostEnforceTypedAsync<TResult>(
        MethodInfo targetMethod, object?[] args,
        Core.Subscription.SubscriptionContext context,
        PostEnforceAttribute attr)
    {
        var result = await Interceptor.PostEnforceAsync(attr, context, async () =>
        {
            var task = (Task<TResult>?)targetMethod.Invoke(Target, args);
            return task is not null ? await task.ConfigureAwait(false) : default;
        }).ConfigureAwait(false);

        return result is TResult typed ? typed : default;
    }

    private object? PostEnforceSyncFallback(
        MethodInfo targetMethod, object?[] args,
        Core.Subscription.SubscriptionContext context,
        PostEnforceAttribute attr)
    {
        return Interceptor.PostEnforceAsync(attr, context, () =>
        {
            var result = targetMethod.Invoke(Target, args);
            return Task.FromResult(result);
        }).GetAwaiter().GetResult();
    }

    private object? DispatchStreaming(
        MethodInfo targetMethod, object?[] args,
        Core.Subscription.SubscriptionContext context,
        Attribute attr)
    {
        var returnType = targetMethod.ReturnType;

        if (!returnType.IsGenericType)
            throw new InvalidOperationException(
                $"Streaming enforcement requires IAsyncEnumerable<T> return type, but {targetMethod.Name} returns {returnType.Name}.");

        var genericDef = returnType.GetGenericTypeDefinition();

        if (genericDef == typeof(Task<>))
        {
            var innerType = returnType.GetGenericArguments()[0];
            if (innerType.IsGenericType && innerType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            {
                var elementType = innerType.GetGenericArguments()[0];
                var method = typeof(SaplProxy<T>)
                    .GetMethod(nameof(StreamingAsyncWrapper), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(elementType);
                return method.Invoke(this, [targetMethod, args, context, attr]);
            }
        }

        if (genericDef == typeof(IAsyncEnumerable<>))
        {
            var elementType = returnType.GetGenericArguments()[0];
            var method = typeof(SaplProxy<T>)
                .GetMethod(nameof(StreamingWrapper), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(elementType);
            return method.Invoke(this, [targetMethod, args, context, attr]);
        }

        throw new InvalidOperationException(
            $"Streaming enforcement requires IAsyncEnumerable<T> return type, but {targetMethod.Name} returns {returnType.Name}.");
    }

    private IAsyncEnumerable<TElement> StreamingWrapper<TElement>(
        MethodInfo targetMethod, object?[] args,
        Core.Subscription.SubscriptionContext context,
        Attribute attr)
    {
        IAsyncEnumerable<TElement> SourceFactory()
        {
            var result = targetMethod.Invoke(Target, args);
            return (IAsyncEnumerable<TElement>)result!;
        }

        return attr switch
        {
            EnforceTillDeniedAttribute tillDenied => Interceptor.EnforceTillDenied<TElement>(tillDenied, context, SourceFactory),
            EnforceDropWhileDeniedAttribute dropWhile => Interceptor.EnforceDropWhileDenied<TElement>(dropWhile, context, SourceFactory),
            EnforceRecoverableIfDeniedAttribute recoverable => Interceptor.EnforceRecoverableIfDenied<TElement>(recoverable, context, SourceFactory),
            _ => throw new InvalidOperationException($"Unknown streaming enforcement attribute: {attr.GetType().Name}"),
        };
    }

    private async Task<IAsyncEnumerable<TElement>> StreamingAsyncWrapper<TElement>(
        MethodInfo targetMethod, object?[] args,
        Core.Subscription.SubscriptionContext context,
        Attribute attr)
    {
        var task = (Task<IAsyncEnumerable<TElement>>?)targetMethod.Invoke(Target, args);
        var source = task is not null ? await task.ConfigureAwait(false) : EmptyAsyncEnumerable<TElement>();

        IAsyncEnumerable<TElement> SourceFactory() => source;

        return attr switch
        {
            EnforceTillDeniedAttribute tillDenied => Interceptor.EnforceTillDenied<TElement>(tillDenied, context, SourceFactory),
            EnforceDropWhileDeniedAttribute dropWhile => Interceptor.EnforceDropWhileDenied<TElement>(dropWhile, context, SourceFactory),
            EnforceRecoverableIfDeniedAttribute recoverable => Interceptor.EnforceRecoverableIfDenied<TElement>(recoverable, context, SourceFactory),
            _ => throw new InvalidOperationException($"Unknown streaming enforcement attribute: {attr.GetType().Name}"),
        };
    }

#pragma warning disable CS1998
    private static async IAsyncEnumerable<TElement> EmptyAsyncEnumerable<TElement>()
    {
        yield break;
    }
#pragma warning restore CS1998
}
