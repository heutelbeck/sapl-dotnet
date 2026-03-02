using Microsoft.Extensions.Logging;

namespace Sapl.Core.Constraints;

public sealed class StreamingConstraintHandlerBundle : ConstraintHandlerBundle
{
    private readonly List<Action> _onCompleteHandlers = [];
    private readonly List<Action> _onCancelHandlers = [];
    private readonly ILogger _logger;

    public StreamingConstraintHandlerBundle(ILogger logger) : base(logger)
    {
        _logger = logger;
    }

    internal void AddOnCompleteRunnable(Action handler, bool isObligation)
    {
        _onCompleteHandlers.Add(isObligation ? WrapObligation(handler) : WrapAdvice(handler));
    }

    internal void AddOnCancelRunnable(Action handler, bool isObligation)
    {
        _onCancelHandlers.Add(isObligation ? WrapObligation(handler) : WrapAdvice(handler));
    }

    public void HandleOnCompleteConstraints()
    {
        foreach (var handler in _onCompleteHandlers)
        {
            handler();
        }
    }

    public void HandleOnCancelConstraints()
    {
        foreach (var handler in _onCancelHandlers)
        {
            handler();
        }
    }

    private Action WrapObligation(Action handler) => () =>
    {
        try
        { handler(); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Streaming obligation handler failed.");
        }
    };

    private Action WrapAdvice(Action handler) => () =>
    {
        try
        { handler(); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Streaming advice handler failed, continuing.");
        }
    };
}
