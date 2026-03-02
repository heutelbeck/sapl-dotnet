using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Sapl.Core.Constraints.Api;

namespace Sapl.Core.Constraints.Providers;

public sealed class ContentFilteringProvider : IMappingConstraintHandlerProvider
{
    internal const string ErrorPrototypePollution = "Rejected path segment that could cause prototype pollution: ";

    private static readonly HashSet<string> DangerousSegments =
        ["__proto__", "constructor", "prototype", "__class__", "__dict__", "__globals__", "__builtins__"];

    private readonly ILogger<ContentFilteringProvider> _logger;

    public ContentFilteringProvider(ILogger<ContentFilteringProvider> logger)
    {
        _logger = logger;
    }

    public int Priority => 0;

    public bool IsResponsible(JsonElement constraint) =>
        constraint.ValueKind == JsonValueKind.Object &&
        constraint.TryGetProperty("type", out var typeProp) &&
        typeProp.GetString() == "filterJsonContent";

    public Func<object, object> GetHandler(JsonElement constraint)
    {
        if (!constraint.TryGetProperty("actions", out var actions) ||
            actions.ValueKind != JsonValueKind.Array)
        {
            return value => value;
        }

        var filterActions = new List<FilterAction>();
        foreach (var action in actions.EnumerateArray())
        {
            var parsed = ParseFilterAction(action);
            if (parsed is not null)
            {
                filterActions.Add(parsed);
            }
        }

        return value =>
        {
            if (value is not JsonElement element)
            {
                var json = JsonSerializer.Serialize(value, Authorization.SerializerDefaults.Options);
                element = JsonDocument.Parse(json).RootElement;
            }

            var node = JsonNode.Parse(element.GetRawText());
            if (node is null)
                return value;

            foreach (var filterAction in filterActions)
            {
                ApplyFilterAction(node, filterAction);
            }

            var resultJson = node.ToJsonString();
            return JsonDocument.Parse(resultJson).RootElement.Clone();
        };
    }

    private FilterAction? ParseFilterAction(JsonElement action)
    {
        if (action.ValueKind != JsonValueKind.Object)
            return null;

        var type = action.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
        var path = action.TryGetProperty("path", out var pathProp) ? pathProp.GetString() : null;

        if (type is null || path is null)
            return null;

        var segments = ParsePath(path);
        if (segments is null)
            return null;

        var discloseLeft = action.TryGetProperty("discloseLeft", out var dl) ? dl.GetInt32() : 0;
        var discloseRight = action.TryGetProperty("discloseRight", out var dr) ? dr.GetInt32() : 0;
        var replacement = action.TryGetProperty("replacement", out var rep) ? rep.Clone() : (JsonElement?)null;

        return new FilterAction(type, segments, discloseLeft, discloseRight, replacement);
    }

    private string[]? ParsePath(string path)
    {
        if (!path.StartsWith("$.", StringComparison.Ordinal))
        {
            _logger.LogWarning("Content filter path must start with '$.'");
            return null;
        }

        var segments = path[2..].Split('.');
        foreach (var segment in segments)
        {
            if (DangerousSegments.Contains(segment))
            {
                _logger.LogWarning("{Error}{Segment}", ErrorPrototypePollution, segment);
                return null;
            }
        }

        return segments;
    }

    private void ApplyFilterAction(JsonNode root, FilterAction action)
    {
        if (root is JsonArray array)
        {
            foreach (var element in array)
            {
                if (element is not null)
                {
                    ApplyFilterActionToNode(element, action);
                }
            }
            return;
        }

        ApplyFilterActionToNode(root, action);
    }

    private void ApplyFilterActionToNode(JsonNode node, FilterAction action)
    {
        var target = NavigateToParent(node, action.Segments);
        if (target is null)
            return;

        var lastSegment = action.Segments[^1];

        if (target is not JsonObject obj || !obj.ContainsKey(lastSegment))
            return;

        switch (action.Type)
        {
            case "blacken":
                var value = obj[lastSegment];
                if (value is JsonValue jv && jv.TryGetValue<string>(out var str))
                {
                    obj[lastSegment] = BlackenString(str, action.DiscloseLeft, action.DiscloseRight);
                }
                break;

            case "delete":
                obj.Remove(lastSegment);
                break;

            case "replace":
                if (action.Replacement.HasValue)
                {
                    obj[lastSegment] = JsonNode.Parse(action.Replacement.Value.GetRawText());
                }
                break;
        }
    }

    private static JsonNode? NavigateToParent(JsonNode root, string[] segments)
    {
        var current = root;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (current is JsonObject obj && obj.ContainsKey(segments[i]))
            {
                current = obj[segments[i]];
            }
            else
            {
                return null;
            }
        }
        return current;
    }

    private static string BlackenString(string value, int discloseLeft, int discloseRight)
    {
        if (discloseLeft + discloseRight >= value.Length)
            return value;

        var left = discloseLeft > 0 ? value[..discloseLeft] : "";
        var right = discloseRight > 0 ? value[^discloseRight..] : "";
        var blackened = new string('X', value.Length - discloseLeft - discloseRight);
        return left + blackened + right;
    }

    private sealed record FilterAction(
        string Type,
        string[] Segments,
        int DiscloseLeft,
        int DiscloseRight,
        JsonElement? Replacement);
}
