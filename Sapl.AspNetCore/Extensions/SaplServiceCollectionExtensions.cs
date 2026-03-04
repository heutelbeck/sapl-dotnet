using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sapl.AspNetCore.Filters;
using Sapl.AspNetCore.Interception;
using Sapl.AspNetCore.Middleware;
using Sapl.Core.Client;
using Sapl.Core.Constraints;
using Sapl.Core.Constraints.Api;
using Sapl.Core.Constraints.Providers;
using Sapl.Core.Enforcement;
using Sapl.Core.Interception;

namespace Sapl.AspNetCore.Extensions;

public static class SaplServiceCollectionExtensions
{
    public static IServiceCollection AddSapl(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "Sapl")
    {
        var options = configuration.GetSection(sectionName).Get<PdpClientOptions>()
            ?? new PdpClientOptions();
        return services.AddSapl(options);
    }

    public static IServiceCollection AddSapl(
        this IServiceCollection services,
        Action<PdpClientOptions> configureOptions)
    {
        var options = new PdpClientOptions();
        configureOptions(options);
        return services.AddSapl(options);
    }

    private static IServiceCollection AddSapl(
        this IServiceCollection services,
        PdpClientOptions options)
    {
        options.Validate();

        services.AddSingleton(options);
        services.AddHttpClient("SaplPdp");
        services.AddSingleton<IPolicyDecisionPoint, PdpClient>();
        services.AddSingleton<ConstraintEnforcementService>();
        services.AddSingleton<EnforcementEngine>();
        services.AddSingleton<SaplMethodInterceptor>();

        services.AddHttpContextAccessor();
        services.AddScoped<HttpSubscriptionContextFactory>();

        services.AddSingleton<IRunnableConstraintHandlerProvider>(
            _ => new EmptyCollectionPlaceholder());
        services.AddSingleton<IConsumerConstraintHandlerProvider>(
            _ => new EmptyCollectionPlaceholder2());
        services.AddSingleton<IMappingConstraintHandlerProvider, ContentFilteringProvider>();
        services.AddSingleton<IFilterPredicateConstraintHandlerProvider, ContentFilterPredicateProvider>();

        services.AddScoped<PreEnforceFilter>();
        services.AddScoped<PostEnforceFilter>();
        services.AddScoped<StreamingEnforcementFilter>();

        services.Configure<MvcOptions>(options =>
        {
            options.Filters.AddService<PreEnforceFilter>();
            options.Filters.AddService<PostEnforceFilter>();
            options.Filters.AddService<StreamingEnforcementFilter>();
        });

        return services;
    }

    public static IServiceCollection AddSaplService<TInterface, TImpl>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TInterface : class
        where TImpl : class, TInterface
    {
        services.Add(new ServiceDescriptor(typeof(TImpl), typeof(TImpl), lifetime));
        services.Add(new ServiceDescriptor(typeof(TInterface), sp =>
        {
            var target = sp.GetRequiredService<TImpl>();
            var proxy = DispatchProxy.Create<TInterface, SaplProxy<TInterface>>();
            var saplProxy = (SaplProxy<TInterface>)(object)proxy;
            saplProxy.Target = target;
            saplProxy.Interceptor = sp.GetRequiredService<SaplMethodInterceptor>();
            saplProxy.ContextFactory = sp.GetRequiredService<HttpSubscriptionContextFactory>();
            return proxy;
        }, lifetime));
        return services;
    }

    public static IServiceCollection AddSaplConstraintHandler<T>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where T : class, IConstraintHandlerProvider
    {
        if (typeof(IRunnableConstraintHandlerProvider).IsAssignableFrom(typeof(T)))
        {
            services.Add(new ServiceDescriptor(typeof(IRunnableConstraintHandlerProvider), typeof(T), lifetime));
        }
        if (typeof(IConsumerConstraintHandlerProvider).IsAssignableFrom(typeof(T)))
        {
            services.Add(new ServiceDescriptor(typeof(IConsumerConstraintHandlerProvider), typeof(T), lifetime));
        }
        if (typeof(IMappingConstraintHandlerProvider).IsAssignableFrom(typeof(T)))
        {
            services.Add(new ServiceDescriptor(typeof(IMappingConstraintHandlerProvider), typeof(T), lifetime));
        }
        if (typeof(IFilterPredicateConstraintHandlerProvider).IsAssignableFrom(typeof(T)))
        {
            services.Add(new ServiceDescriptor(typeof(IFilterPredicateConstraintHandlerProvider), typeof(T), lifetime));
        }
        if (typeof(IErrorHandlerProvider).IsAssignableFrom(typeof(T)))
        {
            services.Add(new ServiceDescriptor(typeof(IErrorHandlerProvider), typeof(T), lifetime));
        }
        if (typeof(IErrorMappingConstraintHandlerProvider).IsAssignableFrom(typeof(T)))
        {
            services.Add(new ServiceDescriptor(typeof(IErrorMappingConstraintHandlerProvider), typeof(T), lifetime));
        }
        if (typeof(IMethodInvocationConstraintHandlerProvider).IsAssignableFrom(typeof(T)))
        {
            services.Add(new ServiceDescriptor(typeof(IMethodInvocationConstraintHandlerProvider), typeof(T), lifetime));
        }

        return services;
    }

    public static IApplicationBuilder UseSaplAccessDenied(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AccessDeniedMiddleware>();
    }

    private sealed class EmptyCollectionPlaceholder : IRunnableConstraintHandlerProvider
    {
        public bool IsResponsible(System.Text.Json.JsonElement constraint) => false;
        public Action GetHandler(System.Text.Json.JsonElement constraint) => () => { };
    }

    private sealed class EmptyCollectionPlaceholder2 : IConsumerConstraintHandlerProvider
    {
        public bool IsResponsible(System.Text.Json.JsonElement constraint) => false;
        public Action<object> GetHandler(System.Text.Json.JsonElement constraint) => _ => { };
    }
}
