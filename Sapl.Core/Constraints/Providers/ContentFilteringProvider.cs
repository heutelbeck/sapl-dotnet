using System.Text.Json;
using Sapl.Core.Constraints.Api;

namespace Sapl.Core.Constraints.Providers;

public sealed class ContentFilteringProvider : IMappingConstraintHandlerProvider
{
    public int Priority => 0;

    public bool IsResponsible(JsonElement constraint) =>
        constraint.ValueKind == JsonValueKind.Object &&
        constraint.TryGetProperty("type", out var typeProp) &&
        typeProp.ValueKind == JsonValueKind.String &&
        typeProp.GetString() == "filterJsonContent";

    public Func<object, object> GetHandler(JsonElement constraint) =>
        ContentFilter.GetHandler(constraint);
}
