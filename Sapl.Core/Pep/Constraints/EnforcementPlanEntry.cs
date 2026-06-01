using System.Diagnostics;
using System.Text.Json;

namespace Sapl.Core.Pep.Constraints;

/// <summary>
/// A scheduled handler in an <see cref="EnforcementPlan"/>. Orders by ascending
/// priority, then by shape Runner before Mapper before Consumer.
/// </summary>
public sealed record EnforcementPlanEntry(
    ConstraintHandler Handler,
    int Priority,
    ConstraintType ConstraintType,
    JsonElement Constraint) : IComparable<EnforcementPlanEntry>
{
    public int CompareTo(EnforcementPlanEntry? other)
    {
        if (other is null)
            return 1;
        var byPriority = Priority.CompareTo(other.Priority);
        return byPriority != 0 ? byPriority : ShapeRank(Handler).CompareTo(ShapeRank(other.Handler));
    }

    private static int ShapeRank(ConstraintHandler handler) => handler switch
    {
        ConstraintHandler.Runner => 0,
        ConstraintHandler.Mapper => 1,
        ConstraintHandler.Consumer => 2,
        _ => throw new UnreachableException(),
    };
}
