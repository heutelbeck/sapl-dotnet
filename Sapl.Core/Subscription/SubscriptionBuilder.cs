using Sapl.Core.Attributes;
using Sapl.Core.Authorization;

namespace Sapl.Core.Subscription;

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
        if (context.Principal?.Identity?.IsAuthenticated == true)
        {
            var claims = context.Principal.Claims
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

        if (context.Properties.TryGetValue("httpMethod", out var httpMethod) && httpMethod is not null)
        {
            result["httpMethod"] = httpMethod;
        }

        return result.Count > 0 ? result : "unknown";
    }

    private static object DefaultResource(SubscriptionContext context)
    {
        var result = new Dictionary<string, object?>();

        if (context.Properties.TryGetValue("path", out var path) && path is not null)
        {
            result["path"] = path;
        }

        if (context.Properties.TryGetValue("params", out var routeParams) && routeParams is not null)
        {
            result["params"] = routeParams;
        }

        if (context.Properties.TryGetValue("query", out var query) && query is not null)
        {
            result["query"] = query;
        }

        return result.Count > 0 ? result : "unknown";
    }

    public static SubscriptionBuilder FromAttribute(PreEnforceAttribute attr)
    {
        var builder = new SubscriptionBuilder();
        ApplyStaticValues(builder, attr.Subject, attr.Action, attr.Resource, attr.Environment, attr.Secrets);
        return builder;
    }

    public static SubscriptionBuilder FromAttribute(PostEnforceAttribute attr)
    {
        var builder = new SubscriptionBuilder();
        ApplyStaticValues(builder, attr.Subject, attr.Action, attr.Resource, attr.Environment, attr.Secrets);
        return builder;
    }

    public static SubscriptionBuilder FromAttribute(EnforceTillDeniedAttribute attr)
    {
        var builder = new SubscriptionBuilder();
        ApplyStaticValues(builder, attr.Subject, attr.Action, attr.Resource, attr.Environment, attr.Secrets);
        return builder;
    }

    public static SubscriptionBuilder FromAttribute(EnforceDropWhileDeniedAttribute attr)
    {
        var builder = new SubscriptionBuilder();
        ApplyStaticValues(builder, attr.Subject, attr.Action, attr.Resource, attr.Environment, attr.Secrets);
        return builder;
    }

    public static SubscriptionBuilder FromAttribute(EnforceRecoverableIfDeniedAttribute attr)
    {
        var builder = new SubscriptionBuilder();
        ApplyStaticValues(builder, attr.Subject, attr.Action, attr.Resource, attr.Environment, attr.Secrets);
        return builder;
    }

    private static void ApplyStaticValues(
        SubscriptionBuilder builder, string? subject, string? action, string? resource, string? environment,
        string? secrets)
    {
        if (subject is not null)
            builder.WithStaticSubject(subject);
        if (action is not null)
            builder.WithStaticAction(action);
        if (resource is not null)
            builder.WithStaticResource(resource);
        if (environment is not null)
            builder.WithStaticEnvironment(environment);
        if (secrets is not null)
            builder.WithStaticSecrets(secrets);
    }
}
