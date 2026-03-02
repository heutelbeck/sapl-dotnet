using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Sapl.Core.Authorization;

namespace Sapl.AspNetCore.Subscription;

public sealed class SubscriptionBuilder
{
    private Func<SubscriptionContext, object?>? _subjectCallback;
    private Func<SubscriptionContext, object?>? _actionCallback;
    private Func<SubscriptionContext, object?>? _resourceCallback;
    private Func<SubscriptionContext, object?>? _environmentCallback;
    private Func<SubscriptionContext, object?>? _secretsCallback;
    private object? _staticSubject;
    private object? _staticAction;
    private object? _staticResource;
    private object? _staticEnvironment;
    private object? _staticSecrets;

    public SubscriptionBuilder WithSubject(Func<SubscriptionContext, object?> callback)
    {
        _subjectCallback = callback;
        return this;
    }

    public SubscriptionBuilder WithAction(Func<SubscriptionContext, object?> callback)
    {
        _actionCallback = callback;
        return this;
    }

    public SubscriptionBuilder WithResource(Func<SubscriptionContext, object?> callback)
    {
        _resourceCallback = callback;
        return this;
    }

    public SubscriptionBuilder WithEnvironment(Func<SubscriptionContext, object?> callback)
    {
        _environmentCallback = callback;
        return this;
    }

    public SubscriptionBuilder WithSecrets(Func<SubscriptionContext, object?> callback)
    {
        _secretsCallback = callback;
        return this;
    }

    public SubscriptionBuilder WithStaticSubject(object? subject)
    {
        _staticSubject = subject;
        return this;
    }

    public SubscriptionBuilder WithStaticAction(object? action)
    {
        _staticAction = action;
        return this;
    }

    public SubscriptionBuilder WithStaticResource(object? resource)
    {
        _staticResource = resource;
        return this;
    }

    public SubscriptionBuilder WithStaticEnvironment(object? environment)
    {
        _staticEnvironment = environment;
        return this;
    }

    public SubscriptionBuilder WithStaticSecrets(object? secrets)
    {
        _staticSecrets = secrets;
        return this;
    }

    public AuthorizationSubscription Build(SubscriptionContext context)
    {
        var subject = _subjectCallback?.Invoke(context) ?? _staticSubject ?? DefaultSubject(context);
        var action = _actionCallback?.Invoke(context) ?? _staticAction ?? DefaultAction(context);
        var resource = _resourceCallback?.Invoke(context) ?? _staticResource ?? DefaultResource(context);
        var environment = _environmentCallback?.Invoke(context) ?? _staticEnvironment;
        var secrets = _secretsCallback?.Invoke(context) ?? _staticSecrets;

        return AuthorizationSubscription.Create(subject, action, resource, environment, secrets);
    }

    private static object DefaultSubject(SubscriptionContext context)
    {
        if (context.HttpContext?.User.Identity?.IsAuthenticated == true)
        {
            var claims = context.HttpContext.User.Claims
                .GroupBy(c => c.Type)
                .ToDictionary(g => g.Key, g => g.Count() == 1 ? (object)g.First().Value : g.Select(c => c.Value).ToArray());
            return claims.Count > 0 ? claims : "anonymous";
        }
        return "anonymous";
    }

    private static object DefaultAction(SubscriptionContext context)
    {
        var result = new Dictionary<string, object?>();

        if (context.MethodName is not null)
        {
            result["method"] = context.MethodName;
        }

        if (context.ClassName is not null)
        {
            result["controller"] = context.ClassName;
        }

        if (context.HttpContext is not null)
        {
            result["httpMethod"] = context.HttpContext.Request.Method;
        }

        return result.Count > 0 ? result : "unknown";
    }

    private static object DefaultResource(SubscriptionContext context)
    {
        if (context.HttpContext is null)
            return "unknown";

        var result = new Dictionary<string, object?>
        {
            ["path"] = context.HttpContext.Request.Path.Value,
        };

        var routeValues = context.HttpContext.Request.RouteValues;
        if (routeValues.Count > 0)
        {
            result["params"] = routeValues.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString());
        }

        var query = context.HttpContext.Request.Query;
        if (query.Count > 0)
        {
            result["query"] = query.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
        }

        return result;
    }

    public static SubscriptionBuilder FromAttribute(
        Attributes.PreEnforceAttribute attr)
    {
        var builder = new SubscriptionBuilder();
        if (attr.Subject is not null)
            builder.WithStaticSubject(attr.Subject);
        if (attr.Action is not null)
            builder.WithStaticAction(attr.Action);
        if (attr.Resource is not null)
            builder.WithStaticResource(attr.Resource);
        if (attr.Environment is not null)
            builder.WithStaticEnvironment(attr.Environment);
        return builder;
    }

    public static SubscriptionBuilder FromAttribute(
        Attributes.PostEnforceAttribute attr)
    {
        var builder = new SubscriptionBuilder();
        if (attr.Subject is not null)
            builder.WithStaticSubject(attr.Subject);
        if (attr.Action is not null)
            builder.WithStaticAction(attr.Action);
        if (attr.Resource is not null)
            builder.WithStaticResource(attr.Resource);
        if (attr.Environment is not null)
            builder.WithStaticEnvironment(attr.Environment);
        return builder;
    }
}
