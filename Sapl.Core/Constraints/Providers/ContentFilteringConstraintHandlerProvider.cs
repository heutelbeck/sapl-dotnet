using System.Text.Json;
using Sapl.Core.Pep.Constraints;

namespace Sapl.Core.Constraints.Providers;

/// <summary>
/// Enforces a "filterJsonContent" obligation as an output mapper that transforms the
/// protected result with the JSONPath blacken / replace / delete actions in
/// <see cref="ContentFilter"/>. Attaches to the output signal the PEP advertises.
/// </summary>
public sealed class ContentFilteringConstraintHandlerProvider : IConstraintHandlerProvider
{
    private const string ConstraintTypeName = "filterJsonContent";

    public IReadOnlyList<ScopedHandler> GetConstraintHandlers(
        JsonElement constraint,
        IReadOnlySet<SignalType> supportedSignals)
    {
        if (!IConstraintHandlerProvider.ConstraintIsOfType(constraint, ConstraintTypeName))
        {
            return [];
        }

        var outputSignal = FindOutputSignal(supportedSignals);
        if (outputSignal is null)
        {
            return [];
        }

        var transform = ContentFilter.GetHandler(constraint);
        var mapper = new ConstraintHandler.Mapper(value => value is null ? null : transform(value));
        return [new ScopedHandler(mapper, outputSignal, 0)];
    }

    private static SignalType? FindOutputSignal(IReadOnlySet<SignalType> supportedSignals)
    {
        foreach (var signal in supportedSignals)
        {
            if (signal.Kind == SignalKind.Output)
            {
                return signal;
            }
        }

        return null;
    }
}
