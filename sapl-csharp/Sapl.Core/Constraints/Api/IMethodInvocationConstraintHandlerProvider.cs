using System.Text.Json;

namespace Sapl.Core.Constraints.Api;

public interface IMethodInvocationConstraintHandlerProvider : IConstraintHandlerProvider
{
    Action<MethodInvocationContext> GetHandler(JsonElement constraint);
}
