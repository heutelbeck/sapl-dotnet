namespace Sapl.Core.Pep;

/// <summary>
/// The outcome of discharging the constraint handlers for one signal: the
/// (possibly transformed or dropped) value carried through the handlers, and
/// whether an obligation handler failed.
/// </summary>
public sealed record EnforcementResult<T>(Maybe<T> Value, bool FailureState);
