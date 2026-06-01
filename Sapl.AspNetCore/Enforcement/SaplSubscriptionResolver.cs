using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Sapl.AspNetCore.Interception;
using Sapl.Core.Authorization;
using Sapl.Core.Subscription;

namespace Sapl.AspNetCore.Enforcement;

/// <summary>
/// Builds an <see cref="AuthorizationSubscription"/> for a controller action from the request
/// context, the attribute's constant values, and an optional <see cref="ISubscriptionCustomizer"/>.
/// The same <see cref="SubscriptionBuilder"/> + customizer path is used at the domain layer, so
/// controller and method enforcement shape subscriptions identically.
/// </summary>
public sealed class SaplSubscriptionResolver(HttpSubscriptionContextFactory contextFactory, IServiceProvider serviceProvider)
{
    public AuthorizationSubscription Resolve(
        MethodInfo method,
        IDictionary<string, object?> actionArguments,
        SubscriptionBuilder builder,
        Type? customizerType,
        object? returnValue = null)
    {
        var context = contextFactory.Create(method, actionArguments.Values.ToArray(), returnValue);
        if (customizerType is not null)
        {
            var customizer = (ISubscriptionCustomizer)ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, customizerType);
            customizer.Customize(context, builder);
        }

        return builder.Build(context);
    }
}
