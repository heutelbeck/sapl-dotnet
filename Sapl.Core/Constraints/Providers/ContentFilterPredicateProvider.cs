using System.Text.Json;
using Sapl.Core.Constraints.Api;

namespace Sapl.Core.Constraints.Providers;

public sealed class ContentFilterPredicateProvider : IFilterPredicateConstraintHandlerProvider
{
    public bool IsResponsible(JsonElement constraint) =>
        constraint.ValueKind == JsonValueKind.Object &&
        constraint.TryGetProperty("type", out var typeProp) &&
        typeProp.ValueKind == JsonValueKind.String &&
        typeProp.GetString() == "jsonContentFilterPredicate";

    public Func<object, bool> GetHandler(JsonElement constraint) =>
        ContentFilter.PredicateFromConditions(constraint);
}
