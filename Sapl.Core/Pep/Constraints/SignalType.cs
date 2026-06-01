namespace Sapl.Core.Pep.Constraints;

/// <summary>
/// Identifies a signal slot a handler attaches to. <see cref="SignalKind.Output"/>
/// is further discriminated by its value <see cref="System.Type"/>, since a PEP may
/// fire outputs of different element types. The other kinds carry a null type.
/// </summary>
public sealed record SignalType(SignalKind Kind, Type? ValueType = null)
{
    /// <summary>True when handlers attached here receive a value.</summary>
    public bool IsValueCarrying =>
        Kind is SignalKind.Decision or SignalKind.Input or SignalKind.Output or SignalKind.Error;

    public static SignalType Decision { get; } = new(SignalKind.Decision);

    public static SignalType Input { get; } = new(SignalKind.Input);

    public static SignalType Error { get; } = new(SignalKind.Error);

    public static SignalType Cancel { get; } = new(SignalKind.Cancel);

    public static SignalType Complete { get; } = new(SignalKind.Complete);

    public static SignalType Termination { get; } = new(SignalKind.Termination);

    /// <summary>The output signal slot for items of <paramref name="valueType"/>.</summary>
    public static SignalType Output(Type valueType) => new(SignalKind.Output, valueType);
}
