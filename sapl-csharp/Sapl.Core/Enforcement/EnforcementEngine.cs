using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sapl.Core.Authorization;
using Sapl.Core.Client;
using Sapl.Core.Constraints;
using Sapl.Core.Constraints.Api;

namespace Sapl.Core.Enforcement;

public sealed class EnforcementEngine
{
    private readonly IPolicyDecisionPoint _pdp;
    private readonly ConstraintEnforcementService _constraintService;
    private readonly ILogger<EnforcementEngine> _logger;

    public EnforcementEngine(
        IPolicyDecisionPoint pdp,
        ConstraintEnforcementService constraintService,
        ILogger<EnforcementEngine> logger)
    {
        _pdp = pdp;
        _constraintService = constraintService;
        _logger = logger;
    }

    public async Task<PreEnforceResult> PreEnforceAsync(
        AuthorizationSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        var decision = await _pdp.DecideOnceAsync(subscription, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("PreEnforce decision: {Decision}", decision.Decision);

        if (decision.Decision != Decision.Permit)
        {
            RunBestEffortHandlers(decision);
            return PreEnforceResult.Denied();
        }

        try
        {
            var bundle = _constraintService.PreEnforceBundleFor(decision);

            bundle.HandleOnDecisionHandlers();
            bundle.CheckFailedObligations();

            return PreEnforceResult.Permitted(bundle);
        }
        catch (AccessDeniedException ex)
        {
            _logger.LogDebug(ex, "PreEnforce denied due to constraint handling.");
            return PreEnforceResult.Denied();
        }
    }

    public async Task<PostEnforceResult<T>> PostEnforceAsync<T>(
        AuthorizationSubscription subscription,
        T value,
        CancellationToken cancellationToken = default)
    {
        var decision = await _pdp.DecideOnceAsync(subscription, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("PostEnforce decision: {Decision}", decision.Decision);

        if (decision.Decision != Decision.Permit)
        {
            RunBestEffortHandlers(decision);
            return PostEnforceResult<T>.Denied();
        }

        try
        {
            var bundle = _constraintService.PostEnforceBundleFor(decision);
            bundle.HandleOnDecisionHandlers();

            object result = value!;

            if (bundle.HasResourceReplacement)
            {
                result = bundle.ResourceReplacement!.Value;
            }

            result = bundle.HandleAllOnNextConstraints(result);
            bundle.CheckFailedObligations();

            if (result is T typedResult)
            {
                return PostEnforceResult<T>.Permitted(typedResult);
            }

            if (result is JsonElement element)
            {
                var deserialized = JsonSerializer.Deserialize<T>(element.GetRawText());
                if (deserialized is not null)
                {
                    return PostEnforceResult<T>.Permitted(deserialized);
                }
            }

            return PostEnforceResult<T>.Permitted((T)result);
        }
        catch (AccessDeniedException ex)
        {
            _logger.LogDebug(ex, "PostEnforce denied due to constraint handling.");
            return PostEnforceResult<T>.Denied();
        }
    }

    public IAsyncEnumerable<T> EnforceTillDenied<T>(
        AuthorizationSubscription subscription,
        Func<IAsyncEnumerable<T>> sourceFactory,
        Action<AuthorizationDecision>? onDeny = null,
        CancellationToken cancellationToken = default)
    {
        var core = new StreamingEnforcementCore(_pdp, _constraintService, _logger);
        return core.EnforceTillDenied(subscription, sourceFactory, onDeny, cancellationToken);
    }

    public IAsyncEnumerable<T> EnforceDropWhileDenied<T>(
        AuthorizationSubscription subscription,
        Func<IAsyncEnumerable<T>> sourceFactory,
        CancellationToken cancellationToken = default)
    {
        var core = new StreamingEnforcementCore(_pdp, _constraintService, _logger);
        return core.EnforceDropWhileDenied(subscription, sourceFactory, cancellationToken);
    }

    public IAsyncEnumerable<T> EnforceRecoverableIfDenied<T>(
        AuthorizationSubscription subscription,
        Func<IAsyncEnumerable<T>> sourceFactory,
        Action<AuthorizationDecision>? onDeny = null,
        Action<AuthorizationDecision>? onRecover = null,
        CancellationToken cancellationToken = default)
    {
        var core = new StreamingEnforcementCore(_pdp, _constraintService, _logger);
        return core.EnforceRecoverableIfDenied(
            subscription, sourceFactory, onDeny, onRecover, cancellationToken);
    }

    private void RunBestEffortHandlers(AuthorizationDecision decision)
    {
        try
        {
            var bundle = _constraintService.BestEffortBundleFor(decision);
            bundle.HandleOnDecisionHandlers();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Best-effort handler execution failed on deny path.");
        }
    }
}

public sealed record PreEnforceResult
{
    private PreEnforceResult(bool isPermitted, ConstraintHandlerBundle? bundle)
    {
        IsPermitted = isPermitted;
        Bundle = bundle;
    }

    public bool IsPermitted { get; }

    public ConstraintHandlerBundle? Bundle { get; }

    public static PreEnforceResult Permitted(ConstraintHandlerBundle bundle) => new(true, bundle);

    public static PreEnforceResult Denied() => new(false, null);
}

public sealed record PostEnforceResult<T>
{
    private PostEnforceResult(bool isPermitted, T? value)
    {
        IsPermitted = isPermitted;
        Value = value;
    }

    public bool IsPermitted { get; }

    public T? Value { get; }

    public static PostEnforceResult<T> Permitted(T value) => new(true, value);

    public static PostEnforceResult<T> Denied() => new(false, default);
}
