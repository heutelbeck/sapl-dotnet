using System.Text.Json;

namespace Sapl.Core.Constraints.Api;

public interface IFilterPredicateConstraintHandlerProvider : IConstraintHandlerProvider
{
    Func<object, bool> GetHandler(JsonElement constraint);
}
