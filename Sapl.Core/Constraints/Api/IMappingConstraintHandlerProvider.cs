using System.Text.Json;

namespace Sapl.Core.Constraints.Api;

public interface IMappingConstraintHandlerProvider : IConstraintHandlerProvider
{
    int Priority => 0;

    Func<object, object> GetHandler(JsonElement constraint);
}
