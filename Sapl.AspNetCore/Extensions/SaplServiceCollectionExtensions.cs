using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sapl.AspNetCore.Enforcement;
using Sapl.AspNetCore.Filters;
using Sapl.AspNetCore.Interception;
using Sapl.AspNetCore.Middleware;
using Sapl.Core.Authorization;
using Sapl.Core.Client;
using Sapl.Core.Constraints.Providers;
using Sapl.Core.Interception;
using Sapl.Core.Pep.Constraints;
using Sapl.Core.Pep.Enforcement;

namespace Sapl.AspNetCore.Extensions;

/// <summary>
/// Registers the SAPL PDP client, the enforcement engine, the built-in content-filtering
/// constraint handler, the global controller filters, and the domain-layer interception
/// infrastructure (DispatchProxy via <see cref="AddSaplService{TInterface,TImpl}"/>).
/// </summary>
public static class SaplServiceCollectionExtensions
{
    public static IServiceCollection AddSapl(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "Sapl") =>
        services.AddSapl(configuration.GetSection(sectionName).Get<PdpClientOptions>() ?? new PdpClientOptions());

    public static IServiceCollection AddSapl(
        this IServiceCollection services,
        Action<PdpClientOptions> configureOptions)
    {
        var options = new PdpClientOptions();
        configureOptions(options);
        return services.AddSapl(options);
    }

    private static IServiceCollection AddSapl(this IServiceCollection services, PdpClientOptions options)
    {
        options.Validate();

        services.AddSingleton(options);
        services.AddHttpClient("SaplPdp");
        services.AddSingleton<IPolicyDecisionPoint, PdpClient>();
        services.AddSingleton<IConstraintHandlerProvider, ContentFilteringConstraintHandlerProvider>();
        services.AddSingleton(serviceProvider => new EnforcementEngine(
            serviceProvider.GetRequiredService<IPolicyDecisionPoint>(),
            serviceProvider.GetServices<IConstraintHandlerProvider>(),
            SerializerDefaults.Options));

        services.AddHttpContextAccessor();
        services.AddScoped<HttpSubscriptionContextFactory>();
        services.AddScoped<SaplSubscriptionResolver>();
        services.AddScoped<SaplMethodInterceptor>();

        services.AddScoped<PreEnforceFilter>();
        services.AddScoped<PostEnforceFilter>();
        services.AddScoped<StreamEnforceFilter>();
        services.Configure<MvcOptions>(mvc =>
        {
            mvc.Filters.AddService<PreEnforceFilter>();
            mvc.Filters.AddService<PostEnforceFilter>();
            mvc.Filters.AddService<StreamEnforceFilter>();
        });

        return services;
    }

    /// <summary>Registers a custom constraint handler provider for the enforcement engine.</summary>
    public static IServiceCollection AddSaplConstraintHandler<T>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where T : class, IConstraintHandlerProvider
    {
        services.Add(new ServiceDescriptor(typeof(IConstraintHandlerProvider), typeof(T), lifetime));
        return services;
    }

    /// <summary>
    /// Registers a service whose interface methods are enforced at the domain layer via a
    /// DispatchProxy. Methods carrying an enforcement attribute are intercepted; others pass through.
    /// </summary>
    public static IServiceCollection AddSaplService<TInterface, TImpl>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TInterface : class
        where TImpl : class, TInterface
    {
        services.Add(new ServiceDescriptor(typeof(TImpl), typeof(TImpl), lifetime));
        services.Add(new ServiceDescriptor(typeof(TInterface), serviceProvider =>
        {
            var proxy = DispatchProxy.Create<TInterface, SaplProxy<TInterface>>();
            var saplProxy = (SaplProxy<TInterface>)(object)proxy;
            saplProxy.Target = serviceProvider.GetRequiredService<TImpl>();
            saplProxy.Interceptor = serviceProvider.GetRequiredService<SaplMethodInterceptor>();
            saplProxy.ContextFactory = serviceProvider.GetRequiredService<HttpSubscriptionContextFactory>();
            return proxy;
        }, lifetime));
        return services;
    }

    public static IApplicationBuilder UseSaplAccessDenied(this IApplicationBuilder app) =>
        app.UseMiddleware<AccessDeniedMiddleware>();
}
