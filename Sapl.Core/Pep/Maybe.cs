namespace Sapl.Core.Pep;

/// <summary>
/// A two-case optional: a value is either <see cref="Present"/> or
/// <see cref="Absent"/>. Used to thread a constraint-handler value through a
/// signal discharge, where a mapper may legitimately drop the value.
/// </summary>
public abstract record Maybe<T>
{
    private Maybe()
    {
    }

    /// <summary>A value is present.</summary>
    public sealed record Present(T Value) : Maybe<T>;

    /// <summary>No value is present.</summary>
    public sealed record Absent : Maybe<T>
    {
        public static readonly Absent Instance = new();
    }

    /// <summary>Wraps <paramref name="value"/> as a present value.</summary>
    public static Maybe<T> Of(T value) => new Present(value);
}
