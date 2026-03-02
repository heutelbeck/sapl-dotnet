using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sapl.Core.Authorization;

namespace Sapl.Core.Constraints;

public class ConstraintHandlerBundle
{
    private readonly ILogger _logger;
    private readonly List<Action> _runnableHandlers = [];
    private readonly List<Action<object>> _consumerHandlers = [];
    private readonly List<Func<object, object>> _mappingHandlers = [];
    private readonly List<Func<object, bool>> _filterPredicateHandlers = [];
    private readonly List<Action<Exception>> _errorHandlers = [];
    private readonly List<Func<Exception, Exception>> _errorMappingHandlers = [];
    private readonly List<Action<Api.MethodInvocationContext>> _methodInvocationHandlers = [];
    private JsonElement? _resourceReplacement;
    private readonly List<string> _failedObligations = [];

    public ConstraintHandlerBundle(ILogger logger)
    {
        _logger = logger;
    }

    public bool HasResourceReplacement => _resourceReplacement.HasValue;

    public JsonElement? ResourceReplacement => _resourceReplacement;

    public bool HasFailedObligations => _failedObligations.Count > 0;

    public IReadOnlyList<string> FailedObligations => _failedObligations;

    internal void SetResourceReplacement(JsonElement resource)
    {
        _resourceReplacement = resource;
    }

    internal void AddRunnable(Action handler, bool isObligation)
    {
        _runnableHandlers.Add(isObligation ? WrapObligation(handler) : WrapAdvice(handler));
    }

    internal void AddConsumer(Action<object> handler, bool isObligation)
    {
        _consumerHandlers.Add(isObligation ? WrapObligationConsumer(handler) : WrapAdviceConsumer(handler));
    }

    internal void AddMapping(Func<object, object> handler, bool isObligation)
    {
        _mappingHandlers.Add(isObligation ? WrapObligationMapping(handler) : WrapAdviceMapping(handler));
    }

    internal void AddFilterPredicate(Func<object, bool> handler, bool isObligation)
    {
        _filterPredicateHandlers.Add(isObligation ? WrapObligationPredicate(handler) : WrapAdvicePredicate(handler));
    }

    internal void AddErrorHandler(Action<Exception> handler, bool isObligation)
    {
        _errorHandlers.Add(isObligation ? WrapObligationErrorHandler(handler) : WrapAdviceErrorHandler(handler));
    }

    internal void AddErrorMapping(Func<Exception, Exception> handler, bool isObligation)
    {
        _errorMappingHandlers.Add(isObligation ? WrapObligationErrorMapping(handler) : WrapAdviceErrorMapping(handler));
    }

    internal void AddMethodInvocation(Action<Api.MethodInvocationContext> handler, bool isObligation)
    {
        _methodInvocationHandlers.Add(isObligation
            ? WrapObligationMethodInvocation(handler)
            : WrapAdviceMethodInvocation(handler));
    }

    public void HandleOnDecisionHandlers()
    {
        foreach (var handler in _runnableHandlers)
        {
            handler();
        }
    }

    public void HandleMethodInvocationHandlers(Api.MethodInvocationContext context)
    {
        foreach (var handler in _methodInvocationHandlers)
        {
            handler(context);
        }
    }

    public object HandleAllOnNextConstraints(object value)
    {
        var result = value;

        foreach (var predicate in _filterPredicateHandlers)
        {
            if (result is System.Collections.IEnumerable enumerable and not string)
            {
                var filtered = new List<object>();
                foreach (var item in enumerable)
                {
                    if (predicate(item))
                    {
                        filtered.Add(item);
                    }
                }
                result = filtered;
            }
            else if (!predicate(result))
            {
                throw new AccessDeniedException("Filter predicate denied access to resource.");
            }
        }

        foreach (var consumer in _consumerHandlers)
        {
            consumer(result);
        }

        foreach (var mapping in _mappingHandlers)
        {
            result = mapping(result);
        }

        return result;
    }

    public Exception HandleAllOnErrorConstraints(Exception error)
    {
        var result = error;

        foreach (var handler in _errorHandlers)
        {
            handler(result);
        }

        foreach (var mapping in _errorMappingHandlers)
        {
            result = mapping(result);
        }

        return result;
    }

    public void CheckFailedObligations()
    {
        if (_failedObligations.Count > 0)
        {
            throw new AccessDeniedException(
                $"Obligation handler(s) failed: {string.Join(", ", _failedObligations)}");
        }
    }

    private Action WrapObligation(Action handler) => () =>
    {
        try
        { handler(); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Obligation runnable handler failed.");
            _failedObligations.Add(ex.Message);
        }
    };

    private Action WrapAdvice(Action handler) => () =>
    {
        try
        { handler(); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Advice runnable handler failed, continuing.");
        }
    };

    private Action<object> WrapObligationConsumer(Action<object> handler) => value =>
    {
        try
        { handler(value); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Obligation consumer handler failed.");
            _failedObligations.Add(ex.Message);
        }
    };

    private Action<object> WrapAdviceConsumer(Action<object> handler) => value =>
    {
        try
        { handler(value); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Advice consumer handler failed, continuing.");
        }
    };

    private Func<object, object> WrapObligationMapping(Func<object, object> handler) => value =>
    {
        try
        { return handler(value); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Obligation mapping handler failed.");
            _failedObligations.Add(ex.Message);
            return value;
        }
    };

    private Func<object, object> WrapAdviceMapping(Func<object, object> handler) => value =>
    {
        try
        { return handler(value); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Advice mapping handler failed, continuing.");
            return value;
        }
    };

    private Func<object, bool> WrapObligationPredicate(Func<object, bool> handler) => value =>
    {
        try
        { return handler(value); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Obligation filter predicate handler failed.");
            _failedObligations.Add(ex.Message);
            return true;
        }
    };

    private Func<object, bool> WrapAdvicePredicate(Func<object, bool> handler) => value =>
    {
        try
        { return handler(value); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Advice filter predicate handler failed, continuing.");
            return true;
        }
    };

    private Action<Exception> WrapObligationErrorHandler(Action<Exception> handler) => error =>
    {
        try
        { handler(error); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Obligation error handler failed.");
            _failedObligations.Add(ex.Message);
        }
    };

    private Action<Exception> WrapAdviceErrorHandler(Action<Exception> handler) => error =>
    {
        try
        { handler(error); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Advice error handler failed, continuing.");
        }
    };

    private Func<Exception, Exception> WrapObligationErrorMapping(Func<Exception, Exception> handler) => error =>
    {
        try
        { return handler(error); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Obligation error mapping handler failed.");
            _failedObligations.Add(ex.Message);
            return error;
        }
    };

    private Func<Exception, Exception> WrapAdviceErrorMapping(Func<Exception, Exception> handler) => error =>
    {
        try
        { return handler(error); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Advice error mapping handler failed, continuing.");
            return error;
        }
    };

    private Action<Api.MethodInvocationContext> WrapObligationMethodInvocation(
        Action<Api.MethodInvocationContext> handler) => context =>
    {
        try
        { handler(context); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Obligation method invocation handler failed.");
            _failedObligations.Add(ex.Message);
        }
    };

    private Action<Api.MethodInvocationContext> WrapAdviceMethodInvocation(
        Action<Api.MethodInvocationContext> handler) => context =>
    {
        try
        { handler(context); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Advice method invocation handler failed, continuing.");
        }
    };
}
