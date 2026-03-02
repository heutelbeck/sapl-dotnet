using System.Text.Json;

namespace Sapl.Core.Constraints.Api;

public interface IErrorHandlerProvider : IConstraintHandlerProvider
{
    Action<Exception> GetHandler(JsonElement constraint);
}
