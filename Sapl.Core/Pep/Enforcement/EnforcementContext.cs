using Sapl.Core.Constraints;
using Sapl.Core.Pep.Constraints;

namespace Sapl.Core.Pep.Enforcement;

/// <summary>
/// A gated pre-enforcement plan. The decision has been made and decision-scoped handlers
/// have already run; the host now discharges the input, output, and error handlers around
/// the protected invocation. Any obligation failure throws <see cref="AccessDeniedException"/>.
/// </summary>
public sealed class EnforcementContext
{
    internal const string DeniedErrorObligationFailed = "Access denied: an error obligation failed.";
    internal const string DeniedInputObligationFailed = "Access denied: an input obligation failed.";
    internal const string DeniedOutputObligationFailed = "Access denied: an output obligation failed.";

    private readonly EnforcementPlan _plan;
    private readonly Type _outputType;

    internal EnforcementContext(EnforcementPlan plan, Type outputType)
    {
        _plan = plan;
        _outputType = outputType;
    }

    /// <summary>Transforms the protected method's arguments before it runs.</summary>
    public IDictionary<string, object?> EnforceInput(IDictionary<string, object?> arguments) =>
        (IDictionary<string, object?>)Discharge(new Signal.Input(arguments), arguments, DeniedInputObligationFailed)!;

    /// <summary>Transforms the protected method's result.</summary>
    public object? EnforceOutput(object? value) =>
        Discharge(new Signal.Output(_outputType, Maybe<object?>.Of(value)), value, DeniedOutputObligationFailed);

    /// <summary>Observes or transforms an exception the protected method raised.</summary>
    public Exception EnforceError(Exception error) =>
        Discharge(new Signal.Error(error), error, DeniedErrorObligationFailed) as Exception ?? error;

    private object? Discharge(Signal signal, object? original, string failureMessage)
    {
        var result = _plan.Execute(signal, false);
        if (result.FailureState)
        {
            throw new AccessDeniedException(failureMessage);
        }

        return result.Value is Maybe<object?>.Present present ? present.Value : original;
    }
}
