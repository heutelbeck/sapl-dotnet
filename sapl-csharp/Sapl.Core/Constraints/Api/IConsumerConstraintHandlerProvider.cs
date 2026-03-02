using System.Text.Json;

namespace Sapl.Core.Constraints.Api;

public interface IConsumerConstraintHandlerProvider : IConstraintHandlerProvider
{
    Action<object> GetHandler(JsonElement constraint);
}
