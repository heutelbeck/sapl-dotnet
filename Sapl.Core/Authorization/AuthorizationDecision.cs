using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sapl.Core.Authorization;

public sealed record AuthorizationDecision : IEquatable<AuthorizationDecision>
{
    private const int MaxEqualityDepth = 20;

    public static readonly AuthorizationDecision PermitInstance = new() { Decision = Decision.Permit };
    public static readonly AuthorizationDecision DenyInstance = new() { Decision = Decision.Deny };
    public static readonly AuthorizationDecision IndeterminateInstance = new() { Decision = Decision.Indeterminate };
    public static readonly AuthorizationDecision NotApplicableInstance = new() { Decision = Decision.NotApplicable };

    [JsonPropertyName("decision")]
    public required Decision Decision { get; init; }

    [JsonPropertyName("obligations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<JsonElement>? Obligations { get; init; }

    [JsonPropertyName("advice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<JsonElement>? Advice { get; init; }

    [JsonPropertyName("resource")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Resource { get; init; }

    public bool HasResource => Resource.HasValue;

    public bool Equals(AuthorizationDecision? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        if (Decision != other.Decision)
            return false;
        if (!JsonElementListEquals(Obligations, other.Obligations))
            return false;
        if (!JsonElementListEquals(Advice, other.Advice))
            return false;
        if (!NullableJsonElementEquals(Resource, other.Resource))
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Decision);
        AddJsonElementListHash(ref hash, Obligations);
        AddJsonElementListHash(ref hash, Advice);
        if (Resource.HasValue)
        {
            hash.Add(Resource.Value.GetRawText());
        }
        return hash.ToHashCode();
    }

    private static bool JsonElementListEquals(IReadOnlyList<JsonElement>? a, IReadOnlyList<JsonElement>? b)
    {
        if (a is null && b is null)
            return true;
        if (a is null || b is null)
            return false;
        if (a.Count != b.Count)
            return false;
        for (var i = 0; i < a.Count; i++)
        {
            if (!DeepJsonEquals(a[i], b[i], 0))
                return false;
        }
        return true;
    }

    private static bool NullableJsonElementEquals(JsonElement? a, JsonElement? b)
    {
        if (!a.HasValue && !b.HasValue)
            return true;
        if (!a.HasValue || !b.HasValue)
            return false;
        return DeepJsonEquals(a.Value, b.Value, 0);
    }

    internal static bool DeepJsonEquals(JsonElement a, JsonElement b, int depth)
    {
        if (depth > MaxEqualityDepth)
            return false;

        if (a.ValueKind != b.ValueKind)
            return false;

        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
                var propsA = 0;
                foreach (var prop in a.EnumerateObject())
                {
                    propsA++;
                    if (!b.TryGetProperty(prop.Name, out var bValue))
                        return false;
                    if (!DeepJsonEquals(prop.Value, bValue, depth + 1))
                        return false;
                }
                var propsB = 0;
                foreach (var _ in b.EnumerateObject())
                    propsB++;
                return propsA == propsB;

            case JsonValueKind.Array:
                var arrayA = a.EnumerateArray();
                var arrayB = b.EnumerateArray();
                using (var enumA = arrayA.GetEnumerator())
                using (var enumB = arrayB.GetEnumerator())
                {
                    while (true)
                    {
                        var hasA = enumA.MoveNext();
                        var hasB = enumB.MoveNext();
                        if (hasA != hasB)
                            return false;
                        if (!hasA)
                            return true;
                        if (!DeepJsonEquals(enumA.Current, enumB.Current, depth + 1))
                            return false;
                    }
                }

            case JsonValueKind.String:
                return a.GetString() == b.GetString();

            case JsonValueKind.Number:
                return a.GetRawText() == b.GetRawText();

            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                return true;

            default:
                return a.GetRawText() == b.GetRawText();
        }
    }

    private static void AddJsonElementListHash(ref HashCode hash, IReadOnlyList<JsonElement>? list)
    {
        if (list is null)
            return;
        hash.Add(list.Count);
        foreach (var element in list)
        {
            hash.Add(element.GetRawText());
        }
    }
}
