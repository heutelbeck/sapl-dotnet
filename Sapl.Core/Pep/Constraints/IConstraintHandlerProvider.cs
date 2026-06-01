using System.Text.Json;

namespace Sapl.Core.Pep.Constraints;

/// <summary>
/// Translates one constraint into the handlers that enforce it. Returns an empty
/// list when the provider does not recognise the constraint, otherwise one or more
/// scoped handlers. The planner schedules each returned handler against its signal
/// independently, so one obligation can drive several handlers across lifecycle points.
/// </summary>
public interface IConstraintHandlerProvider
{
    /// <summary>The handlers that enforce <paramref name="constraint"/>, or an empty list.</summary>
    IReadOnlyList<ScopedHandler> GetConstraintHandlers(JsonElement constraint, IReadOnlySet<SignalType> supportedSignals);

    /// <summary>True when <paramref name="constraint"/> is an object whose "type" field equals <paramref name="expectedType"/>.</summary>
    static bool ConstraintIsOfType(JsonElement constraint, string expectedType) =>
        constraint.ValueKind == JsonValueKind.Object
        && constraint.TryGetProperty("type", out var type)
        && type.ValueKind == JsonValueKind.String
        && type.GetString() == expectedType;

    /// <summary>The string value of a named field, or null when absent or not a string.</summary>
    static string? StringField(JsonElement constraint, string fieldName) =>
        constraint.ValueKind == JsonValueKind.Object
        && constraint.TryGetProperty(fieldName, out var field)
        && field.ValueKind == JsonValueKind.String
            ? field.GetString()
            : null;
}
