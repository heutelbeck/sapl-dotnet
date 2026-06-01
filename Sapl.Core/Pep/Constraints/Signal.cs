using Sapl.Core.Authorization;

namespace Sapl.Core.Pep.Constraints;

/// <summary>
/// A fired signal carrying the data available at that lifecycle point. The plan
/// discharges the handlers scheduled for <see cref="Type"/>.
/// </summary>
public abstract record Signal
{
    private Signal()
    {
    }

    /// <summary>The signal slot whose scheduled handlers this signal discharges.</summary>
    public abstract SignalType Type { get; }

    /// <summary>The decision has arrived and decision-scoped handlers run.</summary>
    public sealed record Decision(AuthorizationDecision Value) : Signal
    {
        public override SignalType Type => SignalType.Decision;
    }

    /// <summary>The protected invocation is about to run; mappers may mutate it in place.</summary>
    public sealed record Input(object? Value) : Signal
    {
        public override SignalType Type => SignalType.Input;
    }

    /// <summary>The protected method produced an output item of <paramref name="ValueType"/>.</summary>
    public sealed record Output(Type ValueType, Maybe<object?> Value) : Signal
    {
        public override SignalType Type => SignalType.Output(ValueType);
    }

    /// <summary>The protected method raised <paramref name="Value"/>.</summary>
    public sealed record Error(Exception Value) : Signal
    {
        public override SignalType Type => SignalType.Error;
    }

    /// <summary>The subscriber canceled.</summary>
    public sealed record Cancel : Signal
    {
        public static readonly Cancel Instance = new();

        public override SignalType Type => SignalType.Cancel;
    }

    /// <summary>The protected method completed normally.</summary>
    public sealed record Complete : Signal
    {
        public static readonly Complete Instance = new();

        public override SignalType Type => SignalType.Complete;
    }

    /// <summary>The subscription terminated for any reason.</summary>
    public sealed record Termination : Signal
    {
        public static readonly Termination Instance = new();

        public override SignalType Type => SignalType.Termination;
    }
}
