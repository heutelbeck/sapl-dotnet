using System.Text.Json;

namespace Sapl.Core.Constraints.Api;

public interface IConstraintHandlerProvider
{
    bool IsResponsible(JsonElement constraint);

    Signal Signal => Signal.OnDecision;
}
