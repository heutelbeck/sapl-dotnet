using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Sapl.Core.Authorization;
using Sapl.Core.Client;
using Sapl.Core.Constraints;

namespace Sapl.Core.Enforcement;

public sealed class StreamingEnforcementCore
{
    private readonly IPolicyDecisionPoint _pdp;
    private readonly ConstraintEnforcementService _constraintService;
    private readonly ILogger _logger;

    public StreamingEnforcementCore(
        IPolicyDecisionPoint pdp,
        ConstraintEnforcementService constraintService,
        ILogger logger)
    {
        _pdp = pdp;
        _constraintService = constraintService;
        _logger = logger;
    }

    public IAsyncEnumerable<T> EnforceTillDenied<T>(
        AuthorizationSubscription subscription,
        Func<IAsyncEnumerable<T>> sourceFactory,
        Action<AuthorizationDecision>? onDeny = null,
        CancellationToken cancellationToken = default)
    {
        return EnforceStreaming(
            subscription, sourceFactory, StreamingMode.TillDenied,
            onDeny, null, cancellationToken);
    }

    public IAsyncEnumerable<T> EnforceDropWhileDenied<T>(
        AuthorizationSubscription subscription,
        Func<IAsyncEnumerable<T>> sourceFactory,
        CancellationToken cancellationToken = default)
    {
        return EnforceStreaming(
            subscription, sourceFactory, StreamingMode.DropWhileDenied,
            null, null, cancellationToken);
    }

    public IAsyncEnumerable<T> EnforceRecoverableIfDenied<T>(
        AuthorizationSubscription subscription,
        Func<IAsyncEnumerable<T>> sourceFactory,
        Action<AuthorizationDecision>? onDeny = null,
        Action<AuthorizationDecision>? onRecover = null,
        CancellationToken cancellationToken = default)
    {
        return EnforceStreaming(
            subscription, sourceFactory, StreamingMode.RecoverableIfDenied,
            onDeny, onRecover, cancellationToken);
    }

    private async IAsyncEnumerable<T> EnforceStreaming<T>(
        AuthorizationSubscription subscription,
        Func<IAsyncEnumerable<T>> sourceFactory,
        StreamingMode mode,
        Action<AuthorizationDecision>? onDeny,
        Action<AuthorizationDecision>? onRecover,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var outputChannel = Channel.CreateBounded<StreamItem<T>>(new BoundedChannelOptions(128)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var processingTask = Task.Run(
            () => RunStreamingLoop(
                subscription, sourceFactory, mode, onDeny, onRecover,
                outputChannel.Writer, cts.Token),
            cts.Token);

        await foreach (var item in outputChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (item.Error is not null)
            {
                cts.Cancel();
                throw item.Error;
            }

            if (item.HasValue)
            {
                yield return item.Value!;
            }
        }

        await processingTask.ConfigureAwait(false);
        cts.Dispose();
    }

    private async Task RunStreamingLoop<T>(
        AuthorizationSubscription subscription,
        Func<IAsyncEnumerable<T>> sourceFactory,
        StreamingMode mode,
        Action<AuthorizationDecision>? onDeny,
        Action<AuthorizationDecision>? onRecover,
        ChannelWriter<StreamItem<T>> writer,
        CancellationToken cancellationToken)
    {
        var accessState = AccessState.Initial;
        StreamingConstraintHandlerBundle? currentBundle = null;
        CancellationTokenSource? sourceCts = null;
        Task? sourceTask = null;
        var terminated = false;

        try
        {
            await foreach (var decision in _pdp.Decide(subscription, cancellationToken).ConfigureAwait(false))
            {
                if (terminated || cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var previousState = accessState;

                if (decision.Decision == Decision.Permit)
                {
                    StreamingConstraintHandlerBundle newBundle;
                    try
                    {
                        newBundle = _constraintService.StreamingBundleFor(decision);
                        newBundle.HandleOnDecisionHandlers();
                        newBundle.CheckFailedObligations();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Constraint resolution failed on PERMIT, treating as DENY.");
                        accessState = AccessState.Denied;
                        currentBundle = null;
                        InvokeOnDeny(onDeny, decision);
                        if (mode == StreamingMode.TillDenied)
                        {
                            terminated = true;
                            await writer.WriteAsync(
                                StreamItem<T>.WithError(new AccessDeniedException("Access denied by PDP.")),
                                cancellationToken).ConfigureAwait(false);
                            break;
                        }
                        continue;
                    }

                    currentBundle = newBundle;
                    accessState = AccessState.Permitted;

                    if (previousState == AccessState.Denied)
                    {
                        InvokeOnRecover(onRecover, decision);
                    }

                    if (sourceTask is null)
                    {
                        sourceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        var bundleRef = currentBundle;
                        sourceTask = Task.Run(
                            () => ReadSourceStream(
                                sourceFactory, writer, () => currentBundle, () => accessState,
                                () => terminated, sourceCts.Token),
                            sourceCts.Token);
                    }
                }
                else
                {
                    accessState = AccessState.Denied;

                    RunBestEffortStreamingHandlers(decision);
                    currentBundle?.HandleOnCancelConstraints();
                    currentBundle = null;

                    if (mode == StreamingMode.TillDenied)
                    {
                        InvokeOnDeny(onDeny, decision);
                        terminated = true;
                        await writer.WriteAsync(
                            StreamItem<T>.WithError(new AccessDeniedException("Access denied by PDP.")),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }

                    if (previousState != AccessState.Denied)
                    {
                        InvokeOnDeny(onDeny, decision);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Streaming enforcement loop failed.");
            try
            {
                await writer.WriteAsync(StreamItem<T>.WithError(ex), CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Writer may be closed
            }
        }
        finally
        {
            if (sourceTask is not null)
            {
                if (terminated || cancellationToken.IsCancellationRequested)
                {
                    if (sourceCts is not null)
                    {
                        await sourceCts.CancelAsync().ConfigureAwait(false);
                    }
                }

                try
                {
                    await sourceTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }

            currentBundle?.HandleOnCompleteConstraints();
            sourceCts?.Dispose();
            writer.TryComplete();
        }
    }

    private async Task ReadSourceStream<T>(
        Func<IAsyncEnumerable<T>> sourceFactory,
        ChannelWriter<StreamItem<T>> writer,
        Func<StreamingConstraintHandlerBundle?> getBundle,
        Func<AccessState> getAccessState,
        Func<bool> isTerminated,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in sourceFactory().WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (isTerminated() || cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (getAccessState() != AccessState.Permitted)
                {
                    continue;
                }

                var bundle = getBundle();
                if (bundle is null)
                {
                    continue;
                }

                try
                {
                    var transformed = bundle.HandleAllOnNextConstraints(item!);
                    if (transformed is T typedResult)
                    {
                        await writer.WriteAsync(
                            StreamItem<T>.WithValue(typedResult), cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (AccessDeniedException)
                {
                    // Filter predicate denied - skip item
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Constraint handler failed on stream item.");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Source stream failed.");
            try
            {
                await writer.WriteAsync(StreamItem<T>.WithError(ex), CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Writer may be closed
            }
        }
    }

    private void RunBestEffortStreamingHandlers(AuthorizationDecision decision)
    {
        try
        {
            var bundle = _constraintService.StreamingBestEffortBundleFor(decision);
            bundle.HandleOnDecisionHandlers();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Best-effort streaming handler execution failed on deny path.");
        }
    }

    private void InvokeOnDeny(Action<AuthorizationDecision>? onDeny, AuthorizationDecision decision)
    {
        if (onDeny is null)
            return;
        try
        {
            onDeny(decision);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "onDeny callback failed.");
        }
    }

    private void InvokeOnRecover(Action<AuthorizationDecision>? onRecover, AuthorizationDecision decision)
    {
        if (onRecover is null)
            return;
        try
        {
            onRecover(decision);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "onRecover callback failed.");
        }
    }
}

internal enum AccessState
{
    Initial,
    Permitted,
    Denied,
}

internal enum StreamingMode
{
    TillDenied,
    DropWhileDenied,
    RecoverableIfDenied,
}

internal readonly struct StreamItem<T>
{
    private StreamItem(T? value, Exception? error, bool hasValue)
    {
        Value = value;
        Error = error;
        HasValue = hasValue;
    }

    public T? Value { get; }

    public Exception? Error { get; }

    public bool HasValue { get; }

    public static StreamItem<T> WithValue(T value) => new(value, null, true);

    public static StreamItem<T> WithError(Exception error) => new(default, error, false);
}
