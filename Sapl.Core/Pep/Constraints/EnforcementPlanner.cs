using System.Text.Json;
using Sapl.Core.Authorization;
using Sapl.Core.Constraints;

namespace Sapl.Core.Pep.Constraints;

/// <summary>
/// Builds the enforcement plan P(d) for an authorization decision (Algorithm 2).
/// Phase 1 resolves a handler for each obligation and advice via the registered
/// providers, substituting a failure runner when resolution is unresolved, ambiguous,
/// or inadmissible. Phase 2 sorts each per-signal sequence and replaces any same-priority
/// mapper group of length greater than one with failure runners, since mapper composition
/// commutativity cannot be proven. A non-undefined decision resource adds an implicit
/// obligation mapper at the output signal that substitutes the resource for the output.
/// </summary>
public sealed class EnforcementPlanner
{
    private const int SubstitutePriority = 0;
    private const string ErrorCannotMapResource = "Cannot map resource {0} to {1}.";
    private const string ErrorUnhandledObligation = "Unhandled obligation ({0}): {1}";

    private readonly IReadOnlyList<IConstraintHandlerProvider> _providers;
    private readonly JsonSerializerOptions? _jsonOptions;

    public EnforcementPlanner(
        IReadOnlyList<IConstraintHandlerProvider> providers,
        JsonSerializerOptions? jsonOptions = null)
    {
        _providers = providers;
        _jsonOptions = jsonOptions;
    }

    /// <summary>
    /// Builds the plan for <paramref name="decision"/>. <paramref name="supportedSignals"/>
    /// is the set of signals the deployed PEP fires. A handler attached to any other signal
    /// is treated as inadmissible.
    /// </summary>
    public EnforcementPlan Plan(AuthorizationDecision decision, IReadOnlySet<SignalType> supportedSignals)
    {
        var entriesBySignal = new Dictionary<SignalType, List<EnforcementPlanEntry>>();
        ScheduleHandlers(decision.Obligations, ConstraintType.Obligation, supportedSignals, entriesBySignal);
        ScheduleHandlers(decision.Advice, ConstraintType.Advice, supportedSignals, entriesBySignal);
        AddImplicitResourceObligation(decision, FindOutputSignal(supportedSignals), entriesBySignal);
        SortAndEnforceCommutativity(entriesBySignal);
        return new EnforcementPlan(entriesBySignal.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<EnforcementPlanEntry>)pair.Value));
    }

    private void ScheduleHandlers(
        IReadOnlyList<JsonElement>? constraints,
        ConstraintType constraintType,
        IReadOnlySet<SignalType> supportedSignals,
        Dictionary<SignalType, List<EnforcementPlanEntry>> entriesBySignal)
    {
        if (constraints is null)
            return;
        foreach (var constraint in constraints)
            foreach (var assignment in AssignHandlers(constraint, constraintType, supportedSignals))
                ScheduleAt(entriesBySignal, assignment.Signal, assignment.Entry);
    }

    // Exactly one provider must claim a constraint. A claim may carry several handlers,
    // each scoped to its own signal and priority. Any inadmissible handler fails the claim.
    private IReadOnlyList<Assignment> AssignHandlers(
        JsonElement constraint,
        ConstraintType constraintType,
        IReadOnlySet<SignalType> supportedSignals)
    {
        var claims = new List<IReadOnlyList<ScopedHandler>>();
        foreach (var provider in _providers)
        {
            var claim = provider.GetConstraintHandlers(constraint, supportedSignals);
            if (claim.Count > 0)
                claims.Add(claim);
        }

        if (claims.Count == 0)
            return [FailureSubstitute(constraint, constraintType, SubstitutionReason.Unresolved)];
        if (claims.Count > 1)
            return [FailureSubstitute(constraint, constraintType, SubstitutionReason.Ambiguous)];

        var scopedHandlers = claims[0];
        foreach (var scoped in scopedHandlers)
            if (!IsAdmissible(scoped, constraintType, supportedSignals))
                return [FailureSubstitute(constraint, constraintType, SubstitutionReason.Inadmissible)];

        var assignments = new List<Assignment>(scopedHandlers.Count);
        foreach (var scoped in scopedHandlers)
            assignments.Add(new Assignment(
                scoped.SignalType,
                new EnforcementPlanEntry(scoped.Handler, scoped.Priority, constraintType, constraint)));
        return assignments;
    }

    // Admissible when the signal is supported, advice carries no mapper, and mappers and
    // consumers attach only to value-carrying signals while runners attach anywhere.
    private static bool IsAdmissible(
        ScopedHandler scoped,
        ConstraintType constraintType,
        IReadOnlySet<SignalType> supportedSignals)
    {
        if (!supportedSignals.Contains(scoped.SignalType))
            return false;
        if (scoped.Handler is ConstraintHandler.Mapper && constraintType != ConstraintType.Obligation)
            return false;
        return scoped.SignalType.IsValueCarrying || scoped.Handler is ConstraintHandler.Runner;
    }

    private static SignalType? FindOutputSignal(IReadOnlySet<SignalType> supportedSignals)
    {
        foreach (var signal in supportedSignals)
            if (signal.Kind == SignalKind.Output)
                return signal;
        return null;
    }

    private void AddImplicitResourceObligation(
        AuthorizationDecision decision,
        SignalType? outputSignal,
        Dictionary<SignalType, List<EnforcementPlanEntry>> entriesBySignal)
    {
        if (!decision.HasResource)
            return;
        var resource = decision.Resource!.Value;
        if (outputSignal is null)
        {
            var substitute = FailureSubstitute(resource, ConstraintType.Obligation, SubstitutionReason.Inadmissible);
            ScheduleAt(entriesBySignal, substitute.Signal, substitute.Entry);
            return;
        }

        var targetType = outputSignal.ValueType ?? typeof(object);
        var mapper = ResourceSubstitutionMapper(resource, targetType);
        ScheduleAt(
            entriesBySignal,
            outputSignal,
            new EnforcementPlanEntry(mapper, int.MinValue, ConstraintType.Obligation, resource));
    }

    // Ignores the protected method's output and returns the decision resource converted
    // to the output value type. A conversion failure denies through the executor's catch.
    private ConstraintHandler ResourceSubstitutionMapper(JsonElement resource, Type targetType) =>
        new ConstraintHandler.Mapper(_ =>
        {
            try
            {
                return JsonSerializer.Deserialize(resource.GetRawText(), targetType, _jsonOptions);
            }
            catch (Exception exception)
            {
                throw new AccessDeniedException(
                    string.Format(ErrorCannotMapResource, resource, targetType.Name), exception);
            }
        });

    private static void SortAndEnforceCommutativity(Dictionary<SignalType, List<EnforcementPlanEntry>> entriesBySignal)
    {
        foreach (var entries in entriesBySignal.Values)
        {
            entries.Sort();
            ReplaceNonCommutingMapperGroups(entries);
        }
    }

    // Any maximal run of mappers at equal priority of length greater than one is replaced
    // in place by failure runners, since the planner cannot prove their composition commutes.
    private static void ReplaceNonCommutingMapperGroups(List<EnforcementPlanEntry> entries)
    {
        var index = 0;
        while (index < entries.Count)
        {
            if (!IsMapper(entries[index]))
            {
                index++;
                continue;
            }

            var groupPriority = entries[index].Priority;
            var groupEnd = index;
            while (groupEnd + 1 < entries.Count
                   && IsMapper(entries[groupEnd + 1])
                   && entries[groupEnd + 1].Priority == groupPriority)
                groupEnd++;

            if (groupEnd > index)
                for (var i = index; i <= groupEnd; i++)
                    entries[i] = AsNonCommutingSubstitute(entries[i]);
            index = groupEnd + 1;
        }
    }

    private static bool IsMapper(EnforcementPlanEntry entry) => entry.Handler is ConstraintHandler.Mapper;

    private static EnforcementPlanEntry AsNonCommutingSubstitute(EnforcementPlanEntry original) =>
        original with
        {
            Handler = SyntheticFailureRunner(original.Constraint, original.ConstraintType, SubstitutionReason.NonCommutingGroup),
        };

    private static Assignment FailureSubstitute(JsonElement constraint, ConstraintType constraintType, SubstitutionReason reason) =>
        new(
            SignalType.Decision,
            new EnforcementPlanEntry(
                SyntheticFailureRunner(constraint, constraintType, reason),
                SubstitutePriority,
                constraintType,
                constraint));

    // On invocation, an obligation substitute denies and an advice substitute completes.
    private static ConstraintHandler SyntheticFailureRunner(JsonElement constraint, ConstraintType constraintType, SubstitutionReason reason) =>
        new ConstraintHandler.Runner(() =>
        {
            if (constraintType == ConstraintType.Obligation)
                throw new AccessDeniedException(string.Format(ErrorUnhandledObligation, reason, constraint));
        });

    private static void ScheduleAt(
        Dictionary<SignalType, List<EnforcementPlanEntry>> entriesBySignal,
        SignalType signal,
        EnforcementPlanEntry entry)
    {
        if (!entriesBySignal.TryGetValue(signal, out var entries))
        {
            entries = [];
            entriesBySignal[signal] = entries;
        }
        entries.Add(entry);
    }

    private sealed record Assignment(SignalType Signal, EnforcementPlanEntry Entry);

    private enum SubstitutionReason
    {
        Unresolved,
        Ambiguous,
        Inadmissible,
        NonCommutingGroup,
    }
}
