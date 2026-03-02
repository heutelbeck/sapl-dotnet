using System.Text.Json;

namespace Sapl.Core.Constraints.Api;

public interface IErrorMappingConstraintHandlerProvider : IConstraintHandlerProvider
{
    int Priority => 0;

    Func<Exception, Exception> GetHandler(JsonElement constraint);
}
