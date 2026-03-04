using System.Collections;
using System.Text.Json;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Sapl.Core.Constraints.Providers;

internal static class ContentFilter
{
    internal const string ErrorActionsNotAnArray = "'actions' is not an array.";
    internal const string ErrorActionNotAnObject = "An action in 'actions' is not an object.";
    internal const string ErrorConditionsNotAnArray = "'conditions' not an array: ";
    internal const string ErrorConstraintInvalid = "Not a valid constraint. Expected a JSON Object";
    internal const string ErrorConstraintPathNotPresent = "Error evaluating a constraint predicate. The path defined in the constraint is not present in the data.";
    internal const string ErrorConstraintPathNotPresentEnforcement = "Constraint enforcement failed. Error evaluating a constraint predicate. The path defined in the constraint is not present in the data.";
    internal const string ErrorConvertingModifiedObject = "Error converting modified object to original class type.";
    internal const string ErrorLengthNotNumber = "'length' of 'blacken' action is not numeric.";
    internal const string ErrorNoReplacementSpecified = "The constraint indicates a text node to be replaced. However, the action does not specify a 'replacement'.";
    internal const string ErrorPathNotTextual = "The constraint indicates a text node to be blackened. However, the node identified by the path is not a text node.";
    internal const string ErrorPredicateConditionInvalid = "Not a valid predicate condition: ";
    internal const string ErrorPrototypePollution = "Rejected path segment that could cause prototype pollution: ";
    internal const string ErrorRegexUnsafe = "Unsafe regex pattern rejected (potential ReDoS): ";
    internal const string ErrorReplacementNotTextual = "'replacement' of 'blacken' action is not textual.";
    internal const string ErrorUndefinedKey = "An action does not declare '{0}'.";
    internal const string ErrorUnknownAction = "Unknown action type: '{0}'.";
    internal const string ErrorValueNotInteger = "An action's '{0}' is not an integer.";
    internal const string ErrorValueNotTextual = "An action's '{0}' is not textual.";

    private const string BlackSquare = "\u2588";
    private const int BlackenLengthInvalidValue = -1;

    private static readonly Regex RedosAlternationWithQuant = new(@"\([^)|]*\|[^)]*\)[*+]", RegexOptions.Compiled);
    private static readonly Regex RedosNestedBoundedQuant = new(@"\{\d+,\d*}[^{]*\{\d+,\d*}", RegexOptions.Compiled);
    private static readonly Regex RedosNestedQuantifiers = new(@"\([^)]*[*+]\)[*+]", RegexOptions.Compiled);
    private static readonly Regex RedosNestedWildcards = new(@"\([^)*]*\*[^)]*\)[^)*]*\*", RegexOptions.Compiled);

    private static readonly HashSet<string> DangerousSegments =
        ["__proto__", "constructor", "prototype", "__class__", "__dict__", "__globals__", "__builtins__"];

    internal static Func<object, object> GetHandler(JsonElement constraint)
    {
        var predicate = PredicateFromConditions(constraint);
        var transformation = GetTransformationHandler(constraint);

        return payload =>
        {
            if (payload is null)
                return null!;

            if (payload is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
                return MapJsonArrayContents(jsonElement, transformation, predicate);

            if (payload is Array array)
                return MapArrayContents(array, transformation, predicate);

            if (payload is IList list)
                return MapListContents(list, transformation, predicate);

            if (payload is ISet<object> set)
                return MapSetContents(set, transformation, predicate);

            return MapElement(payload, transformation, predicate);
        };
    }

    internal static Func<object, bool> PredicateFromConditions(JsonElement constraint)
    {
        AssertConstraintIsAnObject(constraint);

        Func<object, bool> predicate = _ => true;

        if (!constraint.TryGetProperty("conditions", out var conditions))
            return predicate;

        AssertConditionsIsAnArray(conditions);

        foreach (var condition in conditions.EnumerateArray())
        {
            var newPredicate = ConditionToPredicate(condition);
            var previousPredicate = predicate;
            predicate = x => previousPredicate(x) && newPredicate(x);
        }

        return MapPathNotFoundToException(predicate);
    }

    internal static Func<object, object> GetTransformationHandler(JsonElement constraint)
    {
        return original =>
        {
            if (!constraint.TryGetProperty("actions", out var actions))
                return original;

            if (actions.ValueKind != JsonValueKind.Array)
                throw new AccessConstraintViolationException(ErrorActionsNotAnArray);

            var jToken = ToJToken(original);

            foreach (var action in actions.EnumerateArray())
            {
                ApplyAction(jToken, action);
            }

            try
            {
                return ToJsonElement(jToken);
            }
            catch (Exception e)
            {
                throw new AccessConstraintViolationException(ErrorConvertingModifiedObject, e);
            }
        };
    }

    private static void ApplyAction(JToken root, JsonElement action)
    {
        if (action.ValueKind != JsonValueKind.Object)
            throw new AccessConstraintViolationException(ErrorActionNotAnObject);

        var path = GetTextualValueOfActionKey(action, "path");
        var actionType = GetTextualValueOfActionKey(action, "type").Trim().ToLowerInvariant();

        ValidatePathNotDangerous(path);

        JToken? target;
        try
        {
            target = root.SelectToken(path);
        }
        catch (Exception e)
        {
            throw new AccessConstraintViolationException(ErrorConstraintPathNotPresentEnforcement, e);
        }

        if (target is null)
            throw new AccessConstraintViolationException(ErrorConstraintPathNotPresentEnforcement);

        switch (actionType)
        {
            case "delete":
                target.Parent?.Remove();
                return;
            case "blacken":
                Blacken(target, action);
                return;
            case "replace":
                Replace(target, action);
                return;
        }

        throw new AccessConstraintViolationException(string.Format(ErrorUnknownAction, actionType));
    }

    private static void Replace(JToken target, JsonElement action)
    {
        if (!action.TryGetProperty("replacement", out var replacement))
            throw new AccessConstraintViolationException(ErrorNoReplacementSpecified);

        var replacementToken = JToken.Parse(replacement.GetRawText());
        target.Replace(replacementToken);
    }

    private static void Blacken(JToken target, JsonElement action)
    {
        if (target.Type != JTokenType.String)
            throw new AccessConstraintViolationException(ErrorPathNotTextual);

        var originalString = target.Value<string>()!;
        var replacementString = DetermineReplacementString(action);
        var discloseRight = GetIntegerValueOfActionKeyOrDefaultToZero(action, "discloseRight");
        var discloseLeft = GetIntegerValueOfActionKeyOrDefaultToZero(action, "discloseLeft");
        var blackenLength = DetermineBlackenLength(action);

        var result = BlackenUtil(originalString, replacementString, discloseRight, discloseLeft, blackenLength);
        target.Replace(new JValue(result));
    }

    private static int DetermineBlackenLength(JsonElement action)
    {
        if (!action.TryGetProperty("length", out var lengthProp))
            return BlackenLengthInvalidValue;

        if (lengthProp.ValueKind == JsonValueKind.Number && lengthProp.TryGetInt32(out var length) && length >= 0)
            return length;

        throw new AccessConstraintViolationException(ErrorLengthNotNumber);
    }

    private static string BlackenUtil(string originalString, string replacement, int discloseRight, int discloseLeft, int blackenLength)
    {
        if (discloseLeft + discloseRight >= originalString.Length)
            return originalString;

        var replacedChars = originalString.Length - discloseLeft - discloseRight;
        var finalLength = blackenLength == BlackenLengthInvalidValue ? replacedChars : blackenLength;

        var left = discloseLeft > 0 ? originalString[..discloseLeft] : "";
        var right = discloseRight > 0 ? originalString[(discloseLeft + replacedChars)..] : "";
        var blackened = string.Concat(Enumerable.Repeat(replacement, finalLength));

        return left + blackened + right;
    }

    private static string DetermineReplacementString(JsonElement action)
    {
        if (!action.TryGetProperty("replacement", out var replacementProp))
            return BlackSquare;

        if (replacementProp.ValueKind == JsonValueKind.String)
            return replacementProp.GetString()!;

        throw new AccessConstraintViolationException(ErrorReplacementNotTextual);
    }

    private static string GetTextualValueOfActionKey(JsonElement action, string key)
    {
        var value = GetValueOfActionKey(action, key);

        if (value.ValueKind != JsonValueKind.String)
            throw new AccessConstraintViolationException(string.Format(ErrorValueNotTextual, key));

        return value.GetString()!;
    }

    private static int GetIntegerValueOfActionKeyOrDefaultToZero(JsonElement action, string key)
    {
        if (!action.TryGetProperty(key, out var value))
            return 0;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
            return intValue;

        throw new AccessConstraintViolationException(string.Format(ErrorValueNotInteger, key));
    }

    private static JsonElement GetValueOfActionKey(JsonElement action, string key)
    {
        if (!action.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null)
            throw new AccessConstraintViolationException(string.Format(ErrorUndefinedKey, key));

        return value;
    }

    private static void ValidatePathNotDangerous(string path)
    {
        foreach (var segment in path.Split('.'))
        {
            if (DangerousSegments.Contains(segment))
                throw new AccessConstraintViolationException(ErrorPrototypePollution + segment);
        }
    }

    private static void AssertConstraintIsAnObject(JsonElement constraint)
    {
        if (constraint.ValueKind != JsonValueKind.Object)
            throw new AccessConstraintViolationException(ErrorConstraintInvalid);
    }

    private static void AssertConditionsIsAnArray(JsonElement conditions)
    {
        if (conditions.ValueKind != JsonValueKind.Array)
            throw new AccessConstraintViolationException(ErrorConditionsNotAnArray + conditions);
    }

    private static Func<object, bool> MapPathNotFoundToException(Func<object, bool> predicate)
    {
        return x =>
        {
            try
            {
                return predicate(x);
            }
            catch (AccessConstraintViolationException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new AccessConstraintViolationException(ErrorConstraintPathNotPresent, e);
            }
        };
    }

    private static Func<object, bool> ConditionToPredicate(JsonElement condition)
    {
        if (condition.ValueKind != JsonValueKind.Object)
            throw new AccessConstraintViolationException(ErrorPredicateConditionInvalid + condition);

        if (!condition.TryGetProperty("path", out var pathProp) || pathProp.ValueKind != JsonValueKind.String)
            throw new AccessConstraintViolationException(ErrorPredicateConditionInvalid + condition);

        var path = pathProp.GetString()!;

        if (!condition.TryGetProperty("type", out var typeProp) || typeProp.ValueKind != JsonValueKind.String)
            throw new AccessConstraintViolationException(ErrorPredicateConditionInvalid + condition);

        var type = typeProp.GetString()!;

        if (!condition.TryGetProperty("value", out var valueProp))
            throw new AccessConstraintViolationException(ErrorPredicateConditionInvalid + condition);

        return type switch
        {
            "==" => EqualsCondition(condition, path, valueProp),
            "!=" => Not(EqualsCondition(condition, path, valueProp)),
            ">=" => GeqCondition(condition, path, valueProp),
            "<=" => LeqCondition(condition, path, valueProp),
            "<" => LtCondition(condition, path, valueProp),
            ">" => GtCondition(condition, path, valueProp),
            "=~" => RegexCondition(condition, path, valueProp),
            _ => throw new AccessConstraintViolationException(ErrorPredicateConditionInvalid + condition)
        };
    }

    private static Func<object, bool> Not(Func<object, bool> predicate)
    {
        return x => !predicate(x);
    }

    private static Func<object, bool> EqualsCondition(JsonElement condition, string path, JsonElement valueProp)
    {
        if (valueProp.ValueKind == JsonValueKind.Number)
            return NumberEqCondition(path, valueProp);

        if (valueProp.ValueKind != JsonValueKind.String)
            throw new AccessConstraintViolationException(ErrorPredicateConditionInvalid + condition);

        var conditionValue = valueProp.GetString()!;

        return original =>
        {
            var value = GetValueAtPath(original, path);
            if (value is not string stringValue)
                return false;
            return conditionValue.Equals(stringValue, StringComparison.Ordinal);
        };
    }

    private static Func<object, bool> NumberEqCondition(string path, JsonElement valueProp)
    {
        var conditionValue = valueProp.GetDouble();

        return original =>
        {
            var value = GetValueAtPath(original, path);
            if (!TryGetDouble(value, out var numberValue))
                return false;
            return Math.Abs(conditionValue - numberValue) < double.Epsilon;
        };
    }

    private static Func<object, bool> GeqCondition(JsonElement condition, string path, JsonElement valueProp)
    {
        if (valueProp.ValueKind != JsonValueKind.Number)
            throw new AccessConstraintViolationException(ErrorPredicateConditionInvalid + condition);

        var conditionValue = valueProp.GetDouble();

        return original =>
        {
            var value = GetValueAtPath(original, path);
            if (!TryGetDouble(value, out var numberValue))
                return false;
            return numberValue >= conditionValue;
        };
    }

    private static Func<object, bool> LeqCondition(JsonElement condition, string path, JsonElement valueProp)
    {
        if (valueProp.ValueKind != JsonValueKind.Number)
            throw new AccessConstraintViolationException(ErrorPredicateConditionInvalid + condition);

        var conditionValue = valueProp.GetDouble();

        return original =>
        {
            var value = GetValueAtPath(original, path);
            if (!TryGetDouble(value, out var numberValue))
                return false;
            return numberValue <= conditionValue;
        };
    }

    private static Func<object, bool> LtCondition(JsonElement condition, string path, JsonElement valueProp)
    {
        if (valueProp.ValueKind != JsonValueKind.Number)
            throw new AccessConstraintViolationException(ErrorPredicateConditionInvalid + condition);

        var conditionValue = valueProp.GetDouble();

        return original =>
        {
            var value = GetValueAtPath(original, path);
            if (!TryGetDouble(value, out var numberValue))
                return false;
            return numberValue < conditionValue;
        };
    }

    private static Func<object, bool> GtCondition(JsonElement condition, string path, JsonElement valueProp)
    {
        if (valueProp.ValueKind != JsonValueKind.Number)
            throw new AccessConstraintViolationException(ErrorPredicateConditionInvalid + condition);

        var conditionValue = valueProp.GetDouble();

        return original =>
        {
            var value = GetValueAtPath(original, path);
            if (!TryGetDouble(value, out var numberValue))
                return false;
            return numberValue > conditionValue;
        };
    }

    private static Func<object, bool> RegexCondition(JsonElement condition, string path, JsonElement valueProp)
    {
        if (valueProp.ValueKind != JsonValueKind.String)
            throw new AccessConstraintViolationException(ErrorPredicateConditionInvalid + condition);

        var patternText = valueProp.GetString()!;

        if (IsDangerousRegex(patternText))
            throw new AccessConstraintViolationException(ErrorRegexUnsafe + patternText);

        var regex = new Regex(patternText, RegexOptions.Compiled);

        return original =>
        {
            var value = GetValueAtPath(original, path);
            if (value is not string stringValue)
                return false;
            return regex.IsMatch(stringValue);
        };
    }

    private static object? GetValueAtPath(object original, string path)
    {
        var jToken = ToJToken(original);
        var result = jToken.SelectToken(path);

        if (result is null)
            throw new InvalidOperationException("Path not found: " + path);

        return result.Type switch
        {
            JTokenType.String => result.Value<string>(),
            JTokenType.Integer => result.Value<long>(),
            JTokenType.Float => result.Value<double>(),
            JTokenType.Boolean => result.Value<bool>(),
            JTokenType.Null => null,
            _ => result.ToString()
        };
    }

    private static bool TryGetDouble(object? value, out double result)
    {
        switch (value)
        {
            case int i:
                result = i;
                return true;
            case long l:
                result = l;
                return true;
            case float f:
                result = f;
                return true;
            case double d:
                result = d;
                return true;
            case decimal m:
                result = (double)m;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    private static bool IsDangerousRegex(string pattern)
    {
        return RedosNestedQuantifiers.IsMatch(pattern)
               || RedosAlternationWithQuant.IsMatch(pattern)
               || RedosNestedWildcards.IsMatch(pattern)
               || RedosNestedBoundedQuant.IsMatch(pattern)
               || pattern.Contains(".*.*")
               || pattern.Contains(".+.+");
    }

    private static JToken ToJToken(object original)
    {
        if (original is JsonElement element)
            return JToken.Parse(element.GetRawText());

        var json = JsonSerializer.Serialize(original, Authorization.SerializerDefaults.Options);
        return JToken.Parse(json);
    }

    private static JsonElement ToJsonElement(JToken token)
    {
        var json = token.ToString(Newtonsoft.Json.Formatting.None);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static JsonElement MapJsonArrayContents(JsonElement jsonArray, Func<object, object> transformation, Func<object, bool> predicate)
    {
        var results = new List<JsonElement>();
        foreach (var element in jsonArray.EnumerateArray())
        {
            var mapped = MapElement(element, transformation, predicate);
            if (mapped is JsonElement mappedElement)
            {
                results.Add(mappedElement);
            }
            else
            {
                var json = JsonSerializer.Serialize(mapped, Authorization.SerializerDefaults.Options);
                using var doc = JsonDocument.Parse(json);
                results.Add(doc.RootElement.Clone());
            }
        }

        var arrayJson = "[" + string.Join(",", results.Select(r => r.GetRawText())) + "]";
        using var arrayDoc = JsonDocument.Parse(arrayJson);
        return arrayDoc.RootElement.Clone();
    }

    private static object MapElement(object payload, Func<object, object> transformation, Func<object, bool> predicate)
    {
        if (predicate(payload))
            return transformation(payload);

        return payload;
    }

    private static IList MapListContents(IList payload, Func<object, object> transformation, Func<object, bool> predicate)
    {
        var result = new List<object?>(payload.Count);
        foreach (var item in payload)
        {
            result.Add(item is not null ? MapElement(item, transformation, predicate) : null);
        }
        return result;
    }

    private static ISet<object> MapSetContents(ISet<object> payload, Func<object, object> transformation, Func<object, bool> predicate)
    {
        var result = new HashSet<object>();
        foreach (var item in payload)
        {
            result.Add(MapElement(item, transformation, predicate));
        }
        return result;
    }

    private static Array MapArrayContents(Array payload, Func<object, object> transformation, Func<object, bool> predicate)
    {
        var elementType = payload.GetType().GetElementType() ?? typeof(object);
        var result = Array.CreateInstance(elementType, payload.Length);
        for (var i = 0; i < payload.Length; i++)
        {
            var item = payload.GetValue(i);
            result.SetValue(item is not null ? MapElement(item, transformation, predicate) : null, i);
        }
        return result;
    }
}
