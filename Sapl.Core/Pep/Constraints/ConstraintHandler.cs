namespace Sapl.Core.Pep.Constraints;

/// <summary>
/// A constraint handler in one of three shapes. A Runner has no value, a Consumer
/// observes the value, a Mapper transforms it. Handlers operate on the object
/// value channel, and providers cast at their boundary.
/// </summary>
public abstract record ConstraintHandler
{
    private ConstraintHandler()
    {
    }

    /// <summary>A side effect that ignores the carried value.</summary>
    public sealed record Runner(Action Run) : ConstraintHandler;

    /// <summary>Observes the carried value without changing it.</summary>
    public sealed record Consumer(Action<object?> Accept) : ConstraintHandler;

    /// <summary>Transforms the carried value.</summary>
    public sealed record Mapper(Func<object?, object?> Apply) : ConstraintHandler;
}
