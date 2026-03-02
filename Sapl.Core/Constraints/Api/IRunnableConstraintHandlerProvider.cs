using System.Text.Json;

namespace Sapl.Core.Constraints.Api;

public interface IRunnableConstraintHandlerProvider : IConstraintHandlerProvider
{
    Action GetHandler(JsonElement constraint);
}
