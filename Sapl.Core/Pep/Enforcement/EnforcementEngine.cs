using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Sapl.Core.Authorization;
using Sapl.Core.Client;
using Sapl.Core.Constraints;
using Sapl.Core.Pep.Constraints;
using Sapl.Core.Pep.Streaming;

namespace Sapl.Core.Pep.Enforcement;

/// <summary>
/// Framework-agnostic policy enforcement over the <see cref="EnforcementPlanner"/>
/// and the streaming <see cref="MealyMachine"/>. Hosts (ASP.NET filters) build a
/// subscription and call one of the three entry points; every denial surfaces as
/// <see cref="AccessDeniedException"/>, which the host maps to its 403 response.
/// </summary>
public sealed class EnforcementEngine
{
    internal const string DeniedDecisionEnforcementFailed = "Access denied: decision-scoped enforcement of the permit failed.";
    internal const string DeniedOutputObligationFailed = "Access denied: an output obligation failed.";
    internal const string DeniedSuspended = "Access denied: the policy suspended a non-streaming request.";

    private static readonly IReadOnlySet<SignalType> PreEnforceSignals =
        new HashSet<SignalType> { SignalType.Decision };

    private readonly IPolicyDecisionPoint _pdp;
    private readonly EnforcementPlanner _planner;

    public EnforcementEngine(
        IPolicyDecisionPoint pdp,
        IEnumerable<IConstraintHandlerProvider> providers,
        JsonSerializerOptions? jsonOptions = null)
    {
        _pdp = pdp;
        _planner = new EnforcementPlanner(providers.ToList(), jsonOptions);
    }

    /// <summary>Decides before the protected method runs and gates it. Throws on denial.</summary>
    public async Task PreEnforceAsync(AuthorizationSubscription subscription, CancellationToken cancellationToken = default)
    {
        var decision = await _pdp.DecideOnceAsync(subscription, cancellationToken).ConfigureAwait(false);
        GateOrThrow(decision, _planner.Plan(decision, PreEnforceSignals));
    }

    /// <summary>
    /// Decides and gates before the protected method runs, returning a context the host
    /// uses to discharge input, output, and error handlers around the invocation. The PEP
    /// advertises decision, input, output, and error; <paramref name="outputType"/> is the
    /// declared result type the output handlers target.
    /// </summary>
    public async Task<EnforcementContext> PreDecideAsync(
        AuthorizationSubscription subscription,
        Type outputType,
        CancellationToken cancellationToken = default)
    {
        var decision = await _pdp.DecideOnceAsync(subscription, cancellationToken).ConfigureAwait(false);
        var supported = new HashSet<SignalType>
        {
            SignalType.Decision, SignalType.Input, SignalType.Output(outputType), SignalType.Error,
        };
        var plan = _planner.Plan(decision, supported);
        GateOrThrow(decision, plan);
        return new EnforcementContext(plan, outputType);
    }

    /// <summary>Decides on the produced result and returns it transformed, or throws on denial.</summary>
    public async Task<object?> PostEnforceAsync(
        AuthorizationSubscription subscription,
        object? result,
        Type resultType,
        CancellationToken cancellationToken = default)
    {
        var decision = await _pdp.DecideOnceAsync(subscription, cancellationToken).ConfigureAwait(false);
        var supported = new HashSet<SignalType> { SignalType.Decision, SignalType.Output(resultType) };
        var plan = _planner.Plan(decision, supported);
        GateOrThrow(decision, plan);

        var enforced = plan.Execute(new Signal.Output(resultType, Maybe<object?>.Of(result)), false);
        if (enforced.FailureState)
        {
            throw new AccessDeniedException(DeniedOutputObligationFailed);
        }

        return enforced.Value is Maybe<object?>.Present present ? present.Value : result;
    }

    /// <summary>
    /// Enforces a streaming result. The PDP decision stream and the protected stream feed
    /// the <see cref="MealyMachine"/>: permitted items flow (transformed by output handlers),
    /// suspended items are dropped, and any denial terminates the stream with
    /// <see cref="AccessDeniedException"/>. Boundary crossings stay silent on this typed path.
    /// </summary>
    public async IAsyncEnumerable<T> EnforceStreamAsync<T>(
        AuthorizationSubscription subscription,
        IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var emission in WalkAsync(subscription, typeof(T), source, cancellationToken).ConfigureAwait(false))
        {
            switch (emission)
            {
                case Emission.Emit emit:
                    yield return (T)emit.Value!;
                    break;
                case Emission.EmitError error:
                    throw error.Error;
                case Emission.EmitComplete:
                case Emission.EmitTransition:
                    break;
            }
        }
    }

    /// <summary>
    /// Object-stream variant for hosts that render untyped output (SSE). Data items flow
    /// boxed as <see cref="object"/>. When <paramref name="signalTransitions"/> is true, each
    /// boundary crossing yields its <see cref="TransitionReason"/> so the host can render a
    /// frame, including the stream-opening grant (transparency for the client, matching the
    /// Spring and NestJS demos). A terminal denial yields the <see cref="AccessDeniedException"/>
    /// (rather than throwing) so the host can render a final frame before completing. Non-denial
    /// errors still propagate.
    /// </summary>
    public async IAsyncEnumerable<object?> EnforceStreamObjectsAsync(
        AuthorizationSubscription subscription,
        IAsyncEnumerable<object?> source,
        Type elementType,
        bool signalTransitions,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var emission in WalkAsync(subscription, elementType, source, cancellationToken).ConfigureAwait(false))
        {
            switch (emission)
            {
                case Emission.Emit emit:
                    yield return emit.Value;
                    break;
                case Emission.EmitError(var error):
                    if (error is AccessDeniedException)
                    {
                        yield return error;
                        yield break;
                    }

                    throw error;
                case Emission.EmitTransition transition when signalTransitions:
                    yield return transition.Reason;
                    break;
            }
        }
    }

    /// <summary>
    /// Shared streaming core. Merges the PDP decision stream and the protected stream into the
    /// <see cref="MealyMachine"/> and yields its raw emissions until a terminal step. Callers
    /// project the emissions to their own output contract.
    /// </summary>
    private async IAsyncEnumerable<Emission> WalkAsync<T>(
        AuthorizationSubscription subscription,
        Type elementType,
        IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var supported = new HashSet<SignalType>
        {
            SignalType.Decision, SignalType.Output(elementType), SignalType.Cancel, SignalType.Complete,
        };
        var channel = Channel.CreateUnbounded<Incoming>(new UnboundedChannelOptions { SingleReader = true });
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var pump = PumpAsync(subscription, supported, source, channel.Writer, linkedCts.Token);

        State state = State.Pending.Instance;
        try
        {
            await foreach (var incoming in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                var evt = incoming is Incoming.Item(var payload)
                    ? new Event.RapItem(payload, PlanOf(state).Execute(new Signal.Output(elementType, Maybe<object?>.Of(payload)), false))
                    : ((Incoming.MachineEvent)incoming).Event;

                var step = MealyMachine.Step(state, evt);
                state = step.NewState;
                foreach (var emission in step.Emissions)
                {
                    yield return emission;
                }

                if (step.IsTerminal)
                {
                    yield break;
                }
            }
        }
        finally
        {
            await linkedCts.CancelAsync().ConfigureAwait(false);
            await pump.ConfigureAwait(false);
        }
    }

    private async Task PumpAsync<T>(
        AuthorizationSubscription subscription,
        IReadOnlySet<SignalType> supported,
        IAsyncEnumerable<T> source,
        ChannelWriter<Incoming> writer,
        CancellationToken cancellationToken)
    {
        var decisions = PumpDecisionsAsync(subscription, supported, writer, cancellationToken);
        var items = PumpItemsAsync(source, writer, cancellationToken);
        await Task.WhenAll(decisions, items).ConfigureAwait(false);
        writer.TryComplete();
    }

    private async Task PumpDecisionsAsync(
        AuthorizationSubscription subscription,
        IReadOnlySet<SignalType> supported,
        ChannelWriter<Incoming> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var decision in _pdp.Decide(subscription, cancellationToken).ConfigureAwait(false))
            {
                writer.TryWrite(new Incoming.MachineEvent(Classify(decision, supported)));
            }
        }
        catch (OperationCanceledException)
        {
            writer.TryWrite(new Incoming.MachineEvent(Event.Cancel.Instance));
        }
        catch (Exception exception)
        {
            writer.TryWrite(new Incoming.MachineEvent(new Event.PdpError(exception)));
        }
    }

    private static async Task PumpItemsAsync<T>(
        IAsyncEnumerable<T> source,
        ChannelWriter<Incoming> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                writer.TryWrite(new Incoming.Item(item));
            }

            writer.TryWrite(new Incoming.MachineEvent(Event.RapComplete.Instance));
        }
        catch (OperationCanceledException)
        {
            writer.TryWrite(new Incoming.MachineEvent(Event.Cancel.Instance));
        }
        catch (Exception exception)
        {
            writer.TryWrite(new Incoming.MachineEvent(new Event.RapError(exception)));
        }
    }

    private Event Classify(AuthorizationDecision decision, IReadOnlySet<SignalType> supported)
    {
        var plan = _planner.Plan(decision, supported);
        switch (decision.Decision)
        {
            case Decision.Permit:
                var enforced = plan.Execute(new Signal.Decision(decision), false);
                return enforced.FailureState
                    ? new Event.PdpDeny(decision, plan, DenyKind.PermitNotEnforceable)
                    : new Event.PdpPermit(decision, plan);
            case Decision.Suspend:
                plan.Execute(new Signal.Decision(decision), false);
                return new Event.PdpSuspend(decision, plan, new TransitionReason.Suspended(decision));
            case Decision.Deny:
                return new Event.PdpDeny(decision, plan, DenyKind.PolicyDenied);
            case Decision.NotApplicable:
                return new Event.PdpDeny(decision, plan, DenyKind.NoPolicyApplicable);
            default:
                return new Event.PdpDeny(decision, plan, DenyKind.Indeterminate);
        }
    }

    private static EnforcementPlan PlanOf(State state) =>
        state is State.Permitting permitting ? permitting.Plan : EnforcementPlan.Empty;

    private void GateOrThrow(AuthorizationDecision decision, EnforcementPlan plan)
    {
        if (decision.Decision != Decision.Permit)
        {
            throw new AccessDeniedException(OneShotDenialMessage(decision.Decision));
        }

        if (plan.Execute(new Signal.Decision(decision), false).FailureState)
        {
            throw new AccessDeniedException(DeniedDecisionEnforcementFailed);
        }
    }

    private static string OneShotDenialMessage(Decision decision) => decision switch
    {
        Decision.Suspend => DeniedSuspended,
        Decision.NotApplicable => MealyMachine.DeniedNoPolicyApplicable,
        Decision.Indeterminate => MealyMachine.DeniedIndeterminate,
        _ => MealyMachine.DeniedByPolicy,
    };

    private abstract record Incoming
    {
        public sealed record MachineEvent(Event Event) : Incoming;

        public sealed record Item(object? Payload) : Incoming;
    }
}
