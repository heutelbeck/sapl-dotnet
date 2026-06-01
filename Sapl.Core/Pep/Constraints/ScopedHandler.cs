namespace Sapl.Core.Pep.Constraints;

/// <summary>Pairs a <see cref="ConstraintHandler"/> with the signal it applies to and a sort priority.</summary>
public sealed record ScopedHandler(ConstraintHandler Handler, SignalType SignalType, int Priority);
