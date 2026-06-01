using System.Diagnostics;

namespace Sapl.Core.Pep.Constraints;

/// <summary>
/// The enforcement plan P(d) for an authorization decision. Maps each signal to the
/// ordered handler entries discharged when that signal fires (Algorithm 3).
/// </summary>
public sealed record EnforcementPlan(IReadOnlyDictionary<SignalType, IReadOnlyList<EnforcementPlanEntry>> Entries)
{
    /// <summary>A plan with no scheduled handlers.</summary>
    public static EnforcementPlan Empty { get; } =
        new(new Dictionary<SignalType, IReadOnlyList<EnforcementPlanEntry>>());

    /// <summary>The ordered entries scheduled for <paramref name="signalType"/>, or an empty list.</summary>
    public IReadOnlyList<EnforcementPlanEntry> EntriesFor(SignalType signalType) =>
        Entries.TryGetValue(signalType, out var entries) ? entries : [];

    /// <summary>
    /// Discharges the entries scheduled for <paramref name="signal"/> in order, applying
    /// mappers, consumers, and runners best-effort. A throwing handler is skipped, and an
    /// obligation handler that throws flips the returned failure state. The failure state
    /// only moves from false to true and is seeded from <paramref name="priorFailureState"/>.
    /// </summary>
    public EnforcementResult<object?> Execute(Signal signal, bool priorFailureState)
    {
        var current = InitialValue(signal);
        var failureState = priorFailureState;
        foreach (var entry in EntriesFor(signal.Type))
        {
            try
            {
                current = Apply(entry.Handler, current);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (entry.ConstraintType == ConstraintType.Obligation)
                    failureState = true;
            }
        }

        return new EnforcementResult<object?>(current, failureState);
    }

    private static Maybe<object?> InitialValue(Signal signal) => signal switch
    {
        Signal.Output output => output.Value,
        Signal.Decision decision => Maybe<object?>.Of(decision.Value),
        Signal.Input input => Maybe<object?>.Of(input.Value),
        Signal.Error error => Maybe<object?>.Of(error.Value),
        Signal.Cancel or Signal.Complete or Signal.Termination => Maybe<object?>.Absent.Instance,
        _ => throw new UnreachableException(),
    };

    private static Maybe<object?> Apply(ConstraintHandler handler, Maybe<object?> current) => handler switch
    {
        ConstraintHandler.Runner runner => RunAndPass(runner, current),
        ConstraintHandler.Consumer consumer => ConsumeAndPass(consumer, current),
        ConstraintHandler.Mapper mapper => MapValue(mapper, current),
        _ => throw new UnreachableException(),
    };

    private static Maybe<object?> RunAndPass(ConstraintHandler.Runner runner, Maybe<object?> current)
    {
        runner.Run();
        return current;
    }

    private static Maybe<object?> ConsumeAndPass(ConstraintHandler.Consumer consumer, Maybe<object?> current)
    {
        if (current is Maybe<object?>.Present present)
            consumer.Accept(present.Value);
        return current;
    }

    private static Maybe<object?> MapValue(ConstraintHandler.Mapper mapper, Maybe<object?> current)
    {
        if (current is Maybe<object?>.Present present)
            return Maybe<object?>.Of(mapper.Apply(present.Value));
        return current;
    }
}
