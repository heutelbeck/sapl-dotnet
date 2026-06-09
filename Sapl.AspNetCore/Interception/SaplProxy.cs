using System.Reflection;
using Sapl.Core.Attributes;
using Sapl.Core.Interception;
using Sapl.Core.Subscription;

namespace Sapl.AspNetCore.Interception;

/// <summary>
/// DispatchProxy that applies SAPL enforcement at the domain layer: it intercepts calls to a
/// service interface, reads the method's enforcement attribute, and routes through the
/// SaplMethodInterceptor. Without an attribute the call passes straight through.
/// </summary>
public class SaplProxy<T> : DispatchProxy where T : class
{
    internal T Target { get; set; } = null!;

    internal SaplMethodInterceptor Interceptor { get; set; } = null!;

    internal HttpSubscriptionContextFactory ContextFactory { get; set; } = null!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null)
        {
            return null;
        }

        var preAttribute = targetMethod.GetCustomAttribute<PreEnforceAttribute>();
        var postAttribute = targetMethod.GetCustomAttribute<PostEnforceAttribute>();
        var streamAttribute = targetMethod.GetCustomAttribute<StreamEnforceAttribute>();

        if (preAttribute is null && postAttribute is null && streamAttribute is null)
        {
            return targetMethod.Invoke(Target, args);
        }

        var context = ContextFactory.Create(targetMethod, args);
        var effectiveArgs = args ?? [];

        if (preAttribute is not null)
        {
            return DispatchPreEnforce(targetMethod, effectiveArgs, context, preAttribute);
        }

        if (postAttribute is not null)
        {
            return DispatchPostEnforce(targetMethod, effectiveArgs, context, postAttribute);
        }

        return DispatchStreamEnforce(targetMethod, effectiveArgs, context, streamAttribute!);
    }

    private object? DispatchPreEnforce(MethodInfo targetMethod, object?[] args, SubscriptionContext context, PreEnforceAttribute attribute)
    {
        var returnType = targetMethod.ReturnType;
        var outputType = UnwrapReturnType(returnType);
        var parameterNames = ParameterNames(targetMethod);

        if (returnType == typeof(Task))
        {
            return PreEnforceVoidAsync(targetMethod, args, context, attribute, outputType, parameterNames);
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return GenericInvoke(nameof(PreEnforceTypedAsync), returnType.GetGenericArguments()[0],
                [targetMethod, args, context, attribute, outputType, parameterNames]);
        }

        return PreEnforceSyncFallback(targetMethod, args, context, attribute, outputType, parameterNames);
    }

    private async Task PreEnforceVoidAsync(MethodInfo targetMethod, object?[] args, SubscriptionContext context, PreEnforceAttribute attribute, Type outputType, IReadOnlyList<string> parameterNames) =>
        await Interceptor.PreEnforceAsync(attribute, context, outputType, parameterNames, args, async updatedArgs =>
        {
            if ((Task?)targetMethod.Invoke(Target, updatedArgs) is { } task)
            {
                await task.ConfigureAwait(false);
            }

            return null;
        }).ConfigureAwait(false);

    private async Task<TResult?> PreEnforceTypedAsync<TResult>(MethodInfo targetMethod, object?[] args, SubscriptionContext context, PreEnforceAttribute attribute, Type outputType, IReadOnlyList<string> parameterNames)
    {
        var result = await Interceptor.PreEnforceAsync(attribute, context, outputType, parameterNames, args, async updatedArgs =>
            (Task<TResult>?)targetMethod.Invoke(Target, updatedArgs) is { } task ? await task.ConfigureAwait(false) : default).ConfigureAwait(false);
        return result is TResult typed ? typed : default;
    }

    private object? PreEnforceSyncFallback(MethodInfo targetMethod, object?[] args, SubscriptionContext context, PreEnforceAttribute attribute, Type outputType, IReadOnlyList<string> parameterNames) =>
        Interceptor.PreEnforceAsync(attribute, context, outputType, parameterNames, args,
            updatedArgs => Task.FromResult(targetMethod.Invoke(Target, updatedArgs))).GetAwaiter().GetResult();

    private object? DispatchPostEnforce(MethodInfo targetMethod, object?[] args, SubscriptionContext context, PostEnforceAttribute attribute)
    {
        var returnType = targetMethod.ReturnType;
        var outputType = UnwrapReturnType(returnType);

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return GenericInvoke(nameof(PostEnforceTypedAsync), returnType.GetGenericArguments()[0],
                [targetMethod, args, context, attribute, outputType]);
        }

        return Interceptor.PostEnforceAsync(attribute, context, outputType,
            () => Task.FromResult(targetMethod.Invoke(Target, args))).GetAwaiter().GetResult();
    }

    private async Task<TResult?> PostEnforceTypedAsync<TResult>(MethodInfo targetMethod, object?[] args, SubscriptionContext context, PostEnforceAttribute attribute, Type outputType)
    {
        var result = await Interceptor.PostEnforceAsync(attribute, context, outputType, async () =>
            (Task<TResult>?)targetMethod.Invoke(Target, args) is { } task ? await task.ConfigureAwait(false) : default).ConfigureAwait(false);
        return result is TResult typed ? typed : default;
    }

    private object? DispatchStreamEnforce(MethodInfo targetMethod, object?[] args, SubscriptionContext context, StreamEnforceAttribute attribute)
    {
        var returnType = targetMethod.ReturnType;
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
        {
            var elementType = returnType.GetGenericArguments()[0];

            // An object element type yields the raw enforced stream including the boundary and
            // denial markers in-band; a typed element type cannot carry them, so it gets the
            // typed path (data items only).
            if (elementType == typeof(object))
            {
                return Interceptor.EnforceStreamObjects(
                    attribute, context, () => (IAsyncEnumerable<object?>)targetMethod.Invoke(Target, args)!);
            }

            return GenericInvoke(nameof(StreamingWrapper), elementType, [targetMethod, args, context, attribute]);
        }

        throw new InvalidOperationException(
            $"StreamEnforce requires an IAsyncEnumerable<T> return type, but {targetMethod.Name} returns {returnType.Name}.");
    }

    private IAsyncEnumerable<TElement> StreamingWrapper<TElement>(MethodInfo targetMethod, object?[] args, SubscriptionContext context, StreamEnforceAttribute attribute) =>
        Interceptor.EnforceStream<TElement>(attribute, context, () => (IAsyncEnumerable<TElement>)targetMethod.Invoke(Target, args)!);

    private object? GenericInvoke(string methodName, Type typeArgument, object?[] parameters) =>
        typeof(SaplProxy<T>).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(typeArgument).Invoke(this, parameters);

    private static IReadOnlyList<string> ParameterNames(MethodInfo method) =>
        method.GetParameters().Select(parameter => parameter.Name ?? string.Empty).ToArray();

    private static Type UnwrapReturnType(Type returnType)
    {
        if (returnType == typeof(Task) || returnType == typeof(void))
        {
            return typeof(object);
        }

        if (returnType.IsGenericType)
        {
            var definition = returnType.GetGenericTypeDefinition();
            if (definition == typeof(Task<>) || definition == typeof(ValueTask<>))
            {
                return returnType.GetGenericArguments()[0];
            }
        }

        return returnType;
    }
}
