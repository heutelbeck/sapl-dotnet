using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sapl.Core.Authorization;

namespace Sapl.Core.Client;

public static class ResponseValidator
{
    private static readonly HashSet<string> ValidDecisions =
        ["PERMIT", "DENY", "INDETERMINATE", "NOT_APPLICABLE"];

    internal const string ErrorDecisionFieldMissing = "PDP response has no 'decision' field.";
    internal const string ErrorDecisionNotObject = "PDP response is not a JSON object.";
    internal const string ErrorDecisionValueInvalid = "PDP response has invalid decision value: ";
    internal const string ErrorJsonParseFailed = "Failed to parse PDP response JSON: ";

    public static AuthorizationDecision ValidateDecisionResponse(
        JsonElement element,
        ILogger logger)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            logger.LogWarning(ErrorDecisionNotObject);
            return AuthorizationDecision.IndeterminateInstance;
        }

        if (!element.TryGetProperty("decision", out var decisionProp) ||
            decisionProp.ValueKind != JsonValueKind.String)
        {
            logger.LogWarning(ErrorDecisionFieldMissing);
            return AuthorizationDecision.IndeterminateInstance;
        }

        var decisionStr = decisionProp.GetString()!;
        if (!ValidDecisions.Contains(decisionStr))
        {
            logger.LogWarning("{Error}{Value}", ErrorDecisionValueInvalid, decisionStr);
            return AuthorizationDecision.IndeterminateInstance;
        }

        var decision = decisionStr switch
        {
            "PERMIT" => Decision.Permit,
            "DENY" => Decision.Deny,
            "INDETERMINATE" => Decision.Indeterminate,
            "NOT_APPLICABLE" => Decision.NotApplicable,
            _ => Decision.Indeterminate,
        };

        IReadOnlyList<JsonElement>? obligations = null;
        if (element.TryGetProperty("obligations", out var obProp) &&
            obProp.ValueKind == JsonValueKind.Array)
        {
            obligations = obProp.EnumerateArray().Select(e => e.Clone()).ToList();
        }

        IReadOnlyList<JsonElement>? advice = null;
        if (element.TryGetProperty("advice", out var adProp) &&
            adProp.ValueKind == JsonValueKind.Array)
        {
            advice = adProp.EnumerateArray().Select(e => e.Clone()).ToList();
        }

        JsonElement? resource = null;
        if (element.TryGetProperty("resource", out var resProp))
        {
            resource = resProp.Clone();
        }

        return new AuthorizationDecision
        {
            Decision = decision,
            Obligations = obligations,
            Advice = advice,
            Resource = resource,
        };
    }

    public static AuthorizationDecision ParseDecisionFromJson(
        string rawJson,
        ILogger logger)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            return ValidateDecisionResponse(doc.RootElement, logger);
        }
        catch (JsonException ex)
        {
            logger.LogWarning("{Error}{Message}", ErrorJsonParseFailed, ex.Message);
            return AuthorizationDecision.IndeterminateInstance;
        }
    }

    public static IdentifiableAuthorizationDecision? ParseIdentifiableDecisionFromJson(
        string rawJson,
        ILogger logger)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                logger.LogWarning(ErrorDecisionNotObject);
                return null;
            }

            if (!root.TryGetProperty("authorizationSubscriptionId", out var idProp) ||
                idProp.ValueKind != JsonValueKind.String ||
                string.IsNullOrEmpty(idProp.GetString()))
            {
                logger.LogWarning("PDP response missing authorizationSubscriptionId.");
                return null;
            }

            var subscriptionId = idProp.GetString()!;

            if (!root.TryGetProperty("authorizationDecision", out var decProp))
            {
                logger.LogWarning("PDP response missing authorizationDecision for subscription {Id}.", subscriptionId);
                return null;
            }

            var decision = ValidateDecisionResponse(decProp, logger);

            return new IdentifiableAuthorizationDecision
            {
                SubscriptionId = subscriptionId,
                Decision = decision,
            };
        }
        catch (JsonException ex)
        {
            logger.LogWarning("{Error}{Message}", ErrorJsonParseFailed, ex.Message);
            return null;
        }
    }

    public static MultiAuthorizationDecision? ParseMultiDecisionFromJson(
        string rawJson,
        ILogger logger)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                logger.LogWarning(ErrorDecisionNotObject);
                return null;
            }

            if (!root.TryGetProperty("decisions", out var decisionsProp) ||
                decisionsProp.ValueKind != JsonValueKind.Object)
            {
                logger.LogWarning("PDP multi-decision response missing 'decisions' object.");
                return null;
            }

            var decisions = new Dictionary<string, AuthorizationDecision>();
            foreach (var prop in decisionsProp.EnumerateObject())
            {
                decisions[prop.Name] = ValidateDecisionResponse(prop.Value, logger);
            }

            return new MultiAuthorizationDecision { Decisions = decisions };
        }
        catch (JsonException ex)
        {
            logger.LogWarning("{Error}{Message}", ErrorJsonParseFailed, ex.Message);
            return null;
        }
    }
}
