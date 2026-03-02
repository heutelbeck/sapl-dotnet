using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sapl.Core.Authorization;
using Sapl.Core.Constraints.Api;

namespace Sapl.Core.Constraints;

public sealed class ConstraintEnforcementService
{
    internal const string ErrorUnhandledObligation = "No handler found for obligation: ";
    internal const string WarnUnhandledAdvice = "No handler found for advice: ";

    private readonly IEnumerable<IRunnableConstraintHandlerProvider> _runnableProviders;
    private readonly IEnumerable<IConsumerConstraintHandlerProvider> _consumerProviders;
    private readonly IEnumerable<IMappingConstraintHandlerProvider> _mappingProviders;
    private readonly IEnumerable<IFilterPredicateConstraintHandlerProvider> _filterProviders;
    private readonly IEnumerable<IErrorHandlerProvider> _errorProviders;
    private readonly IEnumerable<IErrorMappingConstraintHandlerProvider> _errorMappingProviders;
    private readonly IEnumerable<IMethodInvocationConstraintHandlerProvider> _methodInvocationProviders;
    private readonly ILogger<ConstraintEnforcementService> _logger;

    public ConstraintEnforcementService(
        IEnumerable<IRunnableConstraintHandlerProvider> runnableProviders,
        IEnumerable<IConsumerConstraintHandlerProvider> consumerProviders,
        IEnumerable<IMappingConstraintHandlerProvider> mappingProviders,
        IEnumerable<IFilterPredicateConstraintHandlerProvider> filterProviders,
        IEnumerable<IErrorHandlerProvider> errorProviders,
        IEnumerable<IErrorMappingConstraintHandlerProvider> errorMappingProviders,
        IEnumerable<IMethodInvocationConstraintHandlerProvider> methodInvocationProviders,
        ILogger<ConstraintEnforcementService> logger)
    {
        _runnableProviders = runnableProviders;
        _consumerProviders = consumerProviders;
        _mappingProviders = mappingProviders;
        _filterProviders = filterProviders;
        _errorProviders = errorProviders;
        _errorMappingProviders = errorMappingProviders;
        _methodInvocationProviders = methodInvocationProviders;
        _logger = logger;
    }

    public ConstraintHandlerBundle PreEnforceBundleFor(AuthorizationDecision decision)
    {
        var bundle = new ConstraintHandlerBundle(_logger);
        ResolveConstraints(decision, bundle, includeMethodInvocation: true, bestEffort: false);
        return bundle;
    }

    public ConstraintHandlerBundle PostEnforceBundleFor(AuthorizationDecision decision)
    {
        var bundle = new ConstraintHandlerBundle(_logger);
        ResolveConstraints(decision, bundle, includeMethodInvocation: false, bestEffort: false);
        return bundle;
    }

    public ConstraintHandlerBundle BestEffortBundleFor(AuthorizationDecision decision)
    {
        var bundle = new ConstraintHandlerBundle(_logger);
        ResolveConstraints(decision, bundle, includeMethodInvocation: false, bestEffort: true);
        return bundle;
    }

    public StreamingConstraintHandlerBundle StreamingBundleFor(AuthorizationDecision decision)
    {
        var bundle = new StreamingConstraintHandlerBundle(_logger);
        ResolveStreamingConstraints(decision, bundle, bestEffort: false);
        return bundle;
    }

    public StreamingConstraintHandlerBundle StreamingBestEffortBundleFor(AuthorizationDecision decision)
    {
        var bundle = new StreamingConstraintHandlerBundle(_logger);
        ResolveStreamingConstraints(decision, bundle, bestEffort: true);
        return bundle;
    }

    private void ResolveConstraints(
        AuthorizationDecision decision,
        ConstraintHandlerBundle bundle,
        bool includeMethodInvocation,
        bool bestEffort)
    {
        if (decision.Resource.HasValue)
        {
            bundle.SetResourceReplacement(decision.Resource.Value);
        }

        ResolveObligations(decision.Obligations, bundle, includeMethodInvocation, bestEffort);
        ResolveAdvice(decision.Advice, bundle, includeMethodInvocation);
    }

    private void ResolveStreamingConstraints(
        AuthorizationDecision decision,
        StreamingConstraintHandlerBundle bundle,
        bool bestEffort)
    {
        if (decision.Resource.HasValue)
        {
            bundle.SetResourceReplacement(decision.Resource.Value);
        }

        ResolveStreamingObligations(decision.Obligations, bundle, bestEffort);
        ResolveStreamingAdvice(decision.Advice, bundle);
    }

    private void ResolveObligations(
        IReadOnlyList<JsonElement>? obligations,
        ConstraintHandlerBundle bundle,
        bool includeMethodInvocation,
        bool bestEffort)
    {
        if (obligations is null)
            return;

        foreach (var obligation in obligations)
        {
            var handled = false;

            handled |= ResolveRunnableHandlers(obligation, bundle, isObligation: true, Signal.OnDecision);
            handled |= ResolveConsumerHandlers(obligation, bundle, isObligation: true);
            handled |= ResolveMappingHandlers(obligation, bundle, isObligation: true);
            handled |= ResolveFilterPredicateHandlers(obligation, bundle, isObligation: true);
            handled |= ResolveErrorHandlers(obligation, bundle, isObligation: true);
            handled |= ResolveErrorMappingHandlers(obligation, bundle, isObligation: true);

            if (includeMethodInvocation)
            {
                handled |= ResolveMethodInvocationHandlers(obligation, bundle, isObligation: true);
            }

            if (!handled && !bestEffort)
            {
                var text = TruncateConstraintText(obligation);
                _logger.LogWarning("{Error}{Constraint}", ErrorUnhandledObligation, text);
                throw new AccessDeniedException(ErrorUnhandledObligation + text);
            }
        }
    }

    private void ResolveAdvice(
        IReadOnlyList<JsonElement>? advice,
        ConstraintHandlerBundle bundle,
        bool includeMethodInvocation)
    {
        if (advice is null)
            return;

        foreach (var adv in advice)
        {
            var handled = false;

            handled |= ResolveRunnableHandlers(adv, bundle, isObligation: false, Signal.OnDecision);
            handled |= ResolveConsumerHandlers(adv, bundle, isObligation: false);
            handled |= ResolveMappingHandlers(adv, bundle, isObligation: false);
            handled |= ResolveFilterPredicateHandlers(adv, bundle, isObligation: false);
            handled |= ResolveErrorHandlers(adv, bundle, isObligation: false);
            handled |= ResolveErrorMappingHandlers(adv, bundle, isObligation: false);

            if (includeMethodInvocation)
            {
                handled |= ResolveMethodInvocationHandlers(adv, bundle, isObligation: false);
            }

            if (!handled)
            {
                _logger.LogDebug("{Warn}{Constraint}", WarnUnhandledAdvice, TruncateConstraintText(adv));
            }
        }
    }

    private void ResolveStreamingObligations(
        IReadOnlyList<JsonElement>? obligations,
        StreamingConstraintHandlerBundle bundle,
        bool bestEffort)
    {
        if (obligations is null)
            return;

        foreach (var obligation in obligations)
        {
            var handled = false;

            foreach (var provider in _runnableProviders.Where(p => p.IsResponsible(obligation)))
            {
                handled = true;
                var handler = provider.GetHandler(obligation);
                switch (provider.Signal)
                {
                    case Signal.OnDecision:
                        bundle.AddRunnable(handler, isObligation: true);
                        break;
                    case Signal.OnComplete:
                        bundle.AddOnCompleteRunnable(handler, isObligation: true);
                        break;
                    case Signal.OnCancel:
                        bundle.AddOnCancelRunnable(handler, isObligation: true);
                        break;
                }
            }

            handled |= ResolveConsumerHandlers(obligation, bundle, isObligation: true);
            handled |= ResolveMappingHandlers(obligation, bundle, isObligation: true);
            handled |= ResolveFilterPredicateHandlers(obligation, bundle, isObligation: true);
            handled |= ResolveErrorHandlers(obligation, bundle, isObligation: true);
            handled |= ResolveErrorMappingHandlers(obligation, bundle, isObligation: true);

            if (!handled && !bestEffort)
            {
                var text = TruncateConstraintText(obligation);
                _logger.LogWarning("{Error}{Constraint}", ErrorUnhandledObligation, text);
                throw new AccessDeniedException(ErrorUnhandledObligation + text);
            }
        }
    }

    private void ResolveStreamingAdvice(
        IReadOnlyList<JsonElement>? advice,
        StreamingConstraintHandlerBundle bundle)
    {
        if (advice is null)
            return;

        foreach (var adv in advice)
        {
            var handled = false;

            foreach (var provider in _runnableProviders.Where(p => p.IsResponsible(adv)))
            {
                handled = true;
                var handler = provider.GetHandler(adv);
                switch (provider.Signal)
                {
                    case Signal.OnDecision:
                        bundle.AddRunnable(handler, isObligation: false);
                        break;
                    case Signal.OnComplete:
                        bundle.AddOnCompleteRunnable(handler, isObligation: false);
                        break;
                    case Signal.OnCancel:
                        bundle.AddOnCancelRunnable(handler, isObligation: false);
                        break;
                }
            }

            handled |= ResolveConsumerHandlers(adv, bundle, isObligation: false);
            handled |= ResolveMappingHandlers(adv, bundle, isObligation: false);
            handled |= ResolveFilterPredicateHandlers(adv, bundle, isObligation: false);
            handled |= ResolveErrorHandlers(adv, bundle, isObligation: false);
            handled |= ResolveErrorMappingHandlers(adv, bundle, isObligation: false);

            if (!handled)
            {
                _logger.LogDebug("{Warn}{Constraint}", WarnUnhandledAdvice, TruncateConstraintText(adv));
            }
        }
    }

    private bool ResolveRunnableHandlers(
        JsonElement constraint,
        ConstraintHandlerBundle bundle,
        bool isObligation,
        Signal signalFilter)
    {
        var handled = false;
        foreach (var provider in _runnableProviders
            .Where(p => p.Signal == signalFilter && p.IsResponsible(constraint)))
        {
            bundle.AddRunnable(provider.GetHandler(constraint), isObligation);
            handled = true;
        }
        return handled;
    }

    private bool ResolveConsumerHandlers(JsonElement constraint, ConstraintHandlerBundle bundle, bool isObligation)
    {
        var handled = false;
        foreach (var provider in _consumerProviders.Where(p => p.IsResponsible(constraint)))
        {
            bundle.AddConsumer(provider.GetHandler(constraint), isObligation);
            handled = true;
        }
        return handled;
    }

    private bool ResolveMappingHandlers(JsonElement constraint, ConstraintHandlerBundle bundle, bool isObligation)
    {
        var handled = false;
        foreach (var provider in _mappingProviders
            .Where(p => p.IsResponsible(constraint))
            .OrderByDescending(p => p.Priority))
        {
            bundle.AddMapping(provider.GetHandler(constraint), isObligation);
            handled = true;
        }
        return handled;
    }

    private bool ResolveFilterPredicateHandlers(JsonElement constraint, ConstraintHandlerBundle bundle, bool isObligation)
    {
        var handled = false;
        foreach (var provider in _filterProviders.Where(p => p.IsResponsible(constraint)))
        {
            bundle.AddFilterPredicate(provider.GetHandler(constraint), isObligation);
            handled = true;
        }
        return handled;
    }

    private bool ResolveErrorHandlers(JsonElement constraint, ConstraintHandlerBundle bundle, bool isObligation)
    {
        var handled = false;
        foreach (var provider in _errorProviders.Where(p => p.IsResponsible(constraint)))
        {
            bundle.AddErrorHandler(provider.GetHandler(constraint), isObligation);
            handled = true;
        }
        return handled;
    }

    private bool ResolveErrorMappingHandlers(JsonElement constraint, ConstraintHandlerBundle bundle, bool isObligation)
    {
        var handled = false;
        foreach (var provider in _errorMappingProviders
            .Where(p => p.IsResponsible(constraint))
            .OrderByDescending(p => p.Priority))
        {
            bundle.AddErrorMapping(provider.GetHandler(constraint), isObligation);
            handled = true;
        }
        return handled;
    }

    private bool ResolveMethodInvocationHandlers(JsonElement constraint, ConstraintHandlerBundle bundle, bool isObligation)
    {
        var handled = false;
        foreach (var provider in _methodInvocationProviders.Where(p => p.IsResponsible(constraint)))
        {
            bundle.AddMethodInvocation(provider.GetHandler(constraint), isObligation);
            handled = true;
        }
        return handled;
    }

    private static string TruncateConstraintText(JsonElement constraint)
    {
        var text = constraint.GetRawText();
        return text.Length > 200 ? text[..200] + "..." : text;
    }
}
