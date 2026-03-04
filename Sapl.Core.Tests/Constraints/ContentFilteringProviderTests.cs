using System.Text.Json;
using FluentAssertions;
using Sapl.Core.Constraints.Providers;

namespace Sapl.Core.Tests.Constraints;

public class ContentFilteringProviderTests
{
    private readonly ContentFilteringProvider _provider = new();

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Theory]
    [MemberData(nameof(IsResponsibleCases))]
    void WhenCheckingResponsibilityThenReturnsExpectedResult(string _, JsonElement constraint, bool expected)
    {
        _provider.IsResponsible(constraint).Should().Be(expected);
    }

    public static IEnumerable<object[]> IsResponsibleCases()
    {
        yield return ["no type field", Parse("{}"), false];
        yield return ["non-textual type", Parse("""{"type": 123}"""), false];
        yield return ["wrong type value", Parse("""{"type": "unrelatedType"}"""), false];
        yield return ["correct type", Parse("""{"type": "filterJsonContent"}"""), true];
    }

    [Fact]
    void WhenNoActionsSpecifiedThenIsIdentity()
    {
        var constraint = Parse("""{"type": "filterJsonContent"}""");
        var handler = _provider.GetHandler(constraint);
        var original = Parse("""{"key1": "value1"}""");

        var result = (JsonElement)handler(original);

        result.GetProperty("key1").GetString().Should().Be("value1");
    }

    [Theory]
    [MemberData(nameof(ActionValidationErrorCases))]
    void WhenMalformedActionThenThrowsError(string _, string constraintJson)
    {
        var constraint = Parse(constraintJson);
        var handler = _provider.GetHandler(constraint);
        var original = Parse("""{"key1": "value1", "key2": "value2"}""");

        var act = () => handler(original);

        act.Should().Throw<AccessConstraintViolationException>();
    }

    public static IEnumerable<object[]> ActionValidationErrorCases()
    {
        yield return ["no action type", """{"type": "filterJsonContent", "actions": [{"path": "$.key1"}]}"""];
        yield return ["no action path", """{"type": "filterJsonContent", "actions": [{"type": "delete"}]}"""];
        yield return ["action not an object", """{"type": "filterJsonContent", "actions": [123]}"""];
        yield return ["actions not an array", """{"type": "filterJsonContent", "actions": 123}"""];
        yield return ["action path not textual", """{"type": "filterJsonContent", "actions": [{"type": "delete", "path": 123}]}"""];
        yield return ["action type not textual", """{"type": "filterJsonContent", "actions": [{"type": 123, "path": "$.key1"}]}"""];
        yield return ["unknown action type", """{"type": "filterJsonContent", "actions": [{"type": "unknown action", "path": "$.key1"}]}"""];
    }

    [Theory]
    [MemberData(nameof(BlackenValidationErrorCases))]
    void WhenBlackenMalformedThenThrowsError(string _, string constraintJson, string originalJson)
    {
        var constraint = Parse(constraintJson);
        var handler = _provider.GetHandler(constraint);
        var original = Parse(originalJson);

        var act = () => handler(original);

        act.Should().Throw<AccessConstraintViolationException>();
    }

    public static IEnumerable<object[]> BlackenValidationErrorCases()
    {
        yield return ["non-textual replacement",
            """{"type": "filterJsonContent", "actions": [{"type": "blacken", "path": "$.key1", "replacement": 123}]}""",
            """{"key1": "value1", "key2": "value2"}"""];
        yield return ["targets non-textual node",
            """{"type": "filterJsonContent", "actions": [{"type": "blacken", "path": "$.key1"}]}""",
            """{"key1": 123, "key2": "value2"}"""];
        yield return ["discloseRight non-integer",
            """{"type": "filterJsonContent", "actions": [{"type": "blacken", "path": "$.key1", "replacement": "X", "discloseRight": null, "discloseLeft": 1}]}""",
            """{"key1": "value1", "key2": "value2"}"""];
        yield return ["discloseLeft non-integer",
            """{"type": "filterJsonContent", "actions": [{"type": "blacken", "path": "$.key1", "replacement": "X", "discloseRight": 1, "discloseLeft": "wrongType"}]}""",
            """{"key1": "value1", "key2": "value2"}"""];
        yield return ["length negative integer",
            """{"type": "filterJsonContent", "actions": [{"type": "blacken", "path": "$.key1", "replacement": "X", "discloseRight": 1, "discloseLeft": 1, "length": -1}]}""",
            """{"key1": "value1", "key2": "value2"}"""];
        yield return ["length non-integer string",
            """{"type": "filterJsonContent", "actions": [{"type": "blacken", "path": "$.key1", "replacement": "X", "discloseRight": 1, "discloseLeft": 1, "length": "LENGTH"}]}""",
            """{"key1": "value1", "key2": "value2"}"""];
    }

    [Theory]
    [MemberData(nameof(BlackenSuccessCases))]
    void WhenBlackeningThenTextIsBlackenedAsExpected(string _, string constraintJson, string expectedKey1)
    {
        var constraint = Parse(constraintJson);
        var handler = _provider.GetHandler(constraint);
        var original = Parse("""{"key1": "value1", "key2": "value2"}""");

        var result = (JsonElement)handler(original);

        result.GetProperty("key1").GetString().Should().Be(expectedKey1);
    }

    public static IEnumerable<object[]> BlackenSuccessCases()
    {
        yield return ["with replacement and disclose params",
            """{"type": "filterJsonContent", "actions": [{"type": "blacken", "path": "$.key1", "replacement": "X", "discloseRight": 1, "discloseLeft": 1}]}""",
            "vXXXX1"];
        yield return ["with defined length",
            """{"type": "filterJsonContent", "actions": [{"type": "blacken", "path": "$.key1", "replacement": "X", "discloseRight": 1, "discloseLeft": 1, "length": 3}]}""",
            "vXXX1"];
        yield return ["with default replacement",
            """{"type": "filterJsonContent", "actions": [{"type": "blacken", "path": "$.key1", "discloseRight": 1, "discloseLeft": 1}]}""",
            "v\u2588\u2588\u2588\u25881"];
        yield return ["string shorter than disclosed range unchanged",
            """{"type": "filterJsonContent", "actions": [{"type": "blacken", "path": "$.key1", "discloseRight": 2, "discloseLeft": 5}]}""",
            "value1"];
        yield return ["no parameters fully blackened",
            """{"type": "filterJsonContent", "actions": [{"type": "blacken", "path": "$.key1"}]}""",
            "\u2588\u2588\u2588\u2588\u2588\u2588"];
    }

    [Fact]
    void WhenDeleteActionSpecifiedThenDataIsRemovedFromJson()
    {
        var constraint = Parse("""{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.key1"}]}""");
        var handler = _provider.GetHandler(constraint);
        var original = Parse("""{"key1": "value1", "key2": "value2"}""");

        var result = (JsonElement)handler(original);

        result.TryGetProperty("key1", out _).Should().BeFalse();
        result.GetProperty("key2").GetString().Should().Be("value2");
    }

    [Fact]
    void WhenPathNotExistingThenAccessConstraintViolationException()
    {
        var constraint = Parse("""{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.key3"}]}""");
        var handler = _provider.GetHandler(constraint);
        var original = Parse("""{"key1": "value1", "key2": "value2"}""");

        var act = () => handler(original);

        act.Should().Throw<AccessConstraintViolationException>();
    }

    [Fact]
    void WhenMultipleActionsThenAllAreExecuted()
    {
        var constraint = Parse("""
            {
                "type": "filterJsonContent",
                "actions": [
                    {"type": "blacken", "path": "$.key1", "replacement": "X", "discloseRight": 1, "discloseLeft": 1},
                    {"type": "delete", "path": "$.key2"}
                ]
            }
            """);
        var handler = _provider.GetHandler(constraint);
        var original = Parse("""{"key1": "value1", "key2": "value2"}""");

        var result = (JsonElement)handler(original);

        result.GetProperty("key1").GetString().Should().Be("vXXXX1");
        result.TryGetProperty("key2", out _).Should().BeFalse();
    }

    [Fact]
    void WhenReplaceActionSpecifiedThenDataIsReplaced()
    {
        var constraint = Parse("""
            {
                "type": "filterJsonContent",
                "actions": [{"type": "replace", "path": "$.key1", "replacement": {"I": "am", "replaced": "value"}}]
            }
            """);
        var handler = _provider.GetHandler(constraint);
        var original = Parse("""{"key1": "value1", "key2": "value2"}""");

        var result = (JsonElement)handler(original);

        result.GetProperty("key1").GetProperty("I").GetString().Should().Be("am");
        result.GetProperty("key1").GetProperty("replaced").GetString().Should().Be("value");
        result.GetProperty("key2").GetString().Should().Be("value2");
    }

    [Fact]
    void WhenReplaceActionHasNoReplacementThenError()
    {
        var constraint = Parse("""{"type": "filterJsonContent", "actions": [{"type": "replace", "path": "$.key1"}]}""");
        var handler = _provider.GetHandler(constraint);
        var original = Parse("""{"key1": "value1", "key2": "value2"}""");

        var act = () => handler(original);

        act.Should().Throw<AccessConstraintViolationException>();
    }

    [Theory]
    [MemberData(nameof(MalformedConstraintCases))]
    void WhenMalformedConstraintThenGetHandlerThrowsException(string _, string constraintJson)
    {
        var constraint = Parse(constraintJson);

        var act = () => _provider.GetHandler(constraint);

        act.Should().Throw<AccessConstraintViolationException>();
    }

    public static IEnumerable<object[]> MalformedConstraintCases()
    {
        yield return ["condition not object",
            """{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.key3"}], "conditions": [1]}"""];
        yield return ["condition no path",
            """{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.key3"}], "conditions": [{}]}"""];
        yield return ["condition >= not a number",
            """{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.key3"}], "conditions": [{"path": "$.key1", "type": ">=", "value": "not a number"}]}"""];
        yield return ["condition <= not a number",
            """{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.key3"}], "conditions": [{"path": "$.key1", "type": "<=", "value": "not a number"}]}"""];
        yield return ["condition < not a number",
            """{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.key3"}], "conditions": [{"path": "$.key1", "type": "<", "value": "not a number"}]}"""];
        yield return ["condition > not a number",
            """{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.key3"}], "conditions": [{"path": "$.key1", "type": ">", "value": "not a number"}]}"""];
        yield return ["condition == not number or text",
            """{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.key3"}], "conditions": [{"path": "$.key1", "type": "==", "value": []}]}"""];
        yield return ["condition regex not text",
            """{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.key3"}], "conditions": [{"path": "$.key1", "type": "=~", "value": []}]}"""];
        yield return ["condition type non-textual",
            """{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.key3"}], "conditions": [{"path": "$.key1", "type": 12, "value": "abc"}]}"""];
        yield return ["constraint non-object", "123"];
        yield return ["conditions not array",
            """{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.key3"}], "conditions": 123}"""];
        yield return ["condition type unknown",
            """{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.key3"}], "conditions": [{"path": "$.key1", "type": "something unknown", "value": "abc"}]}"""];
        yield return ["condition value missing",
            """{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.key3"}], "conditions": [{"path": "$.key1", "type": "=="}]}"""];
        yield return ["condition path value missing",
            """{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.key3"}], "conditions": [{"type": "==", "value": "abc"}]}"""];
        yield return ["condition type missing",
            """{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.key3"}], "conditions": [{"path": "$.key", "value": "abc"}]}"""];
        yield return ["condition path non-textual",
            """{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.key3"}], "conditions": [{"type": "==", "path": 123, "value": "abc"}]}"""];
    }

    [Fact]
    void WhenEmptyConditionsThenActionAppliedAndConditionAlwaysTrue()
    {
        var constraint = Parse("""
            {
                "type": "filterJsonContent",
                "actions": [{"type": "delete", "path": "$.key1"}],
                "conditions": []
            }
            """);
        var handler = _provider.GetHandler(constraint);
        var original = Parse("""{"key1": "value1", "key2": "value2"}""");

        var result = (JsonElement)handler(original);

        result.TryGetProperty("key1", out _).Should().BeFalse();
        result.GetProperty("key2").GetString().Should().Be("value2");
    }

    [Fact]
    void WhenPredicateNotMatchingThenNoModification()
    {
        var constraint = Parse("""
            {
                "type": "filterJsonContent",
                "actions": [{"type": "delete", "path": "$.key1"}],
                "conditions": [{"path": "$.key1", "type": "==", "value": "another value that does not match"}]
            }
            """);
        var handler = _provider.GetHandler(constraint);
        var original = Parse("""{"key1": "value1", "key2": "value2"}""");

        var result = (JsonElement)handler(original);

        result.GetProperty("key1").GetString().Should().Be("value1");
    }

    [Fact]
    void WhenPredicatePathNotExistingThenAccessConstraintViolationException()
    {
        var constraint = Parse("""
            {
                "type": "filterJsonContent",
                "actions": [{"type": "delete", "path": "$.key1"}],
                "conditions": [{"path": "$.a", "type": "=~", "value": "^.BC$"}]
            }
            """);
        var handler = _provider.GetHandler(constraint);
        var original = Parse("""{"key1": "value1", "key2": "value2"}""");

        var act = () => handler(original);

        act.Should().Throw<AccessConstraintViolationException>();
    }

    [Fact]
    void WhenHandlerHandlesNullThenHandlerReturnsNull()
    {
        var constraint = Parse("""{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.key1"}]}""");
        var handler = _provider.GetHandler(constraint);

        handler(null!).Should().BeNull();
    }

    [Fact]
    void WhenHandlerHandlesListThenHandlerReturnsModifiedListContents()
    {
        var constraint = Parse("""{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.key1"}]}""");
        var handler = _provider.GetHandler(constraint);
        var original = new List<object> { Parse("""{"key1": "value1", "key2": "value2"}""") };

        var result = (IList<object?>)handler(original);

        result.Should().HaveCount(1);
        var element = (JsonElement)result[0]!;
        element.TryGetProperty("key1", out _).Should().BeFalse();
        element.GetProperty("key2").GetString().Should().Be("value2");
    }

    [Fact]
    void WhenHandlerHandlesListMultipleConditionsThenModifiesMatchingElements()
    {
        var constraint = Parse("""
            {
                "type": "filterJsonContent",
                "actions": [{"type": "delete", "path": "$.key1"}],
                "conditions": [
                    {"path": "$.key2", "type": "==", "value": "value2"},
                    {"path": "$.key3", "type": "==", "value": "value3"}
                ]
            }
            """);
        var handler = _provider.GetHandler(constraint);
        var original = new List<object>
        {
            Parse("""{"key1": "value1", "key2": "value2", "key3": "value3"}"""),
            Parse("""{"key1": "value1", "key2": "value2", "key3": "value3"}""")
        };

        var result = (IList<object?>)handler(original);

        result.Should().HaveCount(2);
        ((JsonElement)result[0]!).TryGetProperty("key1", out _).Should().BeFalse();
        ((JsonElement)result[1]!).TryGetProperty("key1", out _).Should().BeFalse();
    }

    [Fact]
    void WhenHandlerHandlesListMultipleConditionsAndOnlyOneHoldsThenNoModifications()
    {
        var constraint = Parse("""
            {
                "type": "filterJsonContent",
                "actions": [{"type": "delete", "path": "$.key1"}],
                "conditions": [
                    {"path": "$.key2", "type": "==", "value": "other"},
                    {"path": "$.key3", "type": "==", "value": "value3"}
                ]
            }
            """);
        var handler = _provider.GetHandler(constraint);
        var original = new List<object>
        {
            Parse("""{"key1": "value1", "key2": "value2", "key3": "value3"}""")
        };

        var result = (IList<object?>)handler(original);

        ((JsonElement)result[0]!).GetProperty("key1").GetString().Should().Be("value1");
    }

    [Fact]
    void WhenHandlerNumEqThenModifiesMatchingElements()
    {
        var constraint = Parse("""
            {
                "type": "filterJsonContent",
                "actions": [{"type": "delete", "path": "$.key1"}],
                "conditions": [{"path": "$.key2", "type": "==", "value": 2}]
            }
            """);
        var handler = _provider.GetHandler(constraint);
        var original = new List<object>
        {
            Parse("""{"key1": "value1", "key2": 2, "key3": "value3"}"""),
            Parse("""{"key1": "value1", "key2": 4, "key3": "value3"}""")
        };

        var result = (IList<object?>)handler(original);

        ((JsonElement)result[0]!).TryGetProperty("key1", out _).Should().BeFalse();
        ((JsonElement)result[1]!).GetProperty("key1").GetString().Should().Be("value1");
    }

    [Fact]
    void WhenHandlerEqNumberDataNotNumberThenNoModification()
    {
        var constraint = Parse("""
            {
                "type": "filterJsonContent",
                "actions": [{"type": "delete", "path": "$.key1"}],
                "conditions": [{"path": "$.key2", "type": "==", "value": 2}]
            }
            """);
        var handler = _provider.GetHandler(constraint);
        var original = new List<object>
        {
            Parse("""{"key1": "value1", "key2": "value2", "key3": "value3"}""")
        };

        var result = (IList<object?>)handler(original);

        ((JsonElement)result[0]!).GetProperty("key1").GetString().Should().Be("value1");
    }

    [Fact]
    void WhenHandlerNeqNumberConditionThenModifiesNonMatchingElements()
    {
        var constraint = Parse("""
            {
                "type": "filterJsonContent",
                "actions": [{"type": "delete", "path": "$.key1"}],
                "conditions": [{"path": "$.key2", "type": "!=", "value": 2}]
            }
            """);
        var handler = _provider.GetHandler(constraint);
        var original = new List<object>
        {
            Parse("""{"key1": "value1", "key2": 2, "key3": 3}"""),
            Parse("""{"key1": "value1", "key2": 3, "key3": 3}""")
        };

        var result = (IList<object?>)handler(original);

        ((JsonElement)result[0]!).GetProperty("key1").GetString().Should().Be("value1");
        ((JsonElement)result[1]!).TryGetProperty("key1", out _).Should().BeFalse();
    }

    [Fact]
    void WhenHandlerRegexConditionThenModifiesMatchingElements()
    {
        var constraint = Parse("""
            {
                "type": "filterJsonContent",
                "actions": [{"type": "delete", "path": "$.key1"}],
                "conditions": [{"path": "$.key2", "type": "=~", "value": "^update.*"}]
            }
            """);
        var handler = _provider.GetHandler(constraint);
        var original = new List<object>
        {
            Parse("""{"key1": "value1", "key2": 2, "key3": 3}"""),
            Parse("""{"key1": "value1", "key2": "updateSomething", "key3": 3}""")
        };

        var result = (IList<object?>)handler(original);

        ((JsonElement)result[0]!).GetProperty("key1").GetString().Should().Be("value1");
        ((JsonElement)result[1]!).TryGetProperty("key1", out _).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(ComparisonOperatorCases))]
    void WhenComparisonConditionThenDeletesKey1AtMatchingIndices(string op, bool[] deleteKey1AtIndex)
    {
        var constraint = Parse($$"""
            {
                "type": "filterJsonContent",
                "actions": [{"type": "delete", "path": "$.key1"}],
                "conditions": [{"path": "$.key2", "type": "{{op}}", "value": 3}]
            }
            """);
        var handler = _provider.GetHandler(constraint);
        var original = new List<object>
        {
            Parse("""{"key1": "value1", "key2": 2, "key3": 1}"""),
            Parse("""{"key1": "value1", "key2": "not a number", "key3": 2}"""),
            Parse("""{"key1": "value1", "key2": 3, "key3": 3}"""),
            Parse("""{"key1": "value1", "key2": 6, "key3": 4}""")
        };

        var result = (IList<object?>)handler(original);

        for (var i = 0; i < 4; i++)
        {
            var element = (JsonElement)result[i]!;
            if (deleteKey1AtIndex[i])
                element.TryGetProperty("key1", out _).Should().BeFalse($"index {i} should have key1 deleted");
            else
                element.GetProperty("key1").GetString().Should().Be("value1", $"index {i} should keep key1");
        }
    }

    public static IEnumerable<object[]> ComparisonOperatorCases()
    {
        yield return [">=", new[] { false, false, true, true }];
        yield return ["<=", new[] { true, false, true, false }];
        yield return ["<", new[] { true, false, false, false }];
        yield return [">", new[] { false, false, false, true }];
    }

    [Fact]
    void WhenHandlerHandlesSetThenHandlerReturnsModifiedSetContents()
    {
        var constraint = Parse("""{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.key1"}]}""");
        var handler = _provider.GetHandler(constraint);
        var original = new HashSet<object> { Parse("""{"key1": "value1", "key2": "value2"}""") };

        var result = (ISet<object>)handler(original);

        result.Should().HaveCount(1);
        var element = (JsonElement)result.First();
        element.TryGetProperty("key1", out _).Should().BeFalse();
        element.GetProperty("key2").GetString().Should().Be("value2");
    }

    [Fact]
    void WhenHandlerHandlesArrayThenHandlerReturnsModifiedArrayContents()
    {
        var constraint = Parse("""{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.key1"}]}""");
        var handler = _provider.GetHandler(constraint);
        var original = new[] { Parse("""{"key1": "value1", "key2": "value2"}""") };

        var result = (JsonElement[])handler(original);

        result.Should().HaveCount(1);
        result[0].TryGetProperty("key1", out _).Should().BeFalse();
        result[0].GetProperty("key2").GetString().Should().Be("value2");
    }

    [Fact]
    void WhenReplaceInDictionaryThenDataIsReplaced()
    {
        var constraint = Parse("""
            {
                "type": "filterJsonContent",
                "actions": [{"type": "replace", "path": "$.key1", "replacement": "replaced"}]
            }
            """);
        var handler = _provider.GetHandler(constraint);
        var original = new Dictionary<string, object> { ["key1"] = "value1", ["key2"] = "value2" };

        var result = (JsonElement)handler(original);

        result.GetProperty("key1").GetString().Should().Be("replaced");
        result.GetProperty("key2").GetString().Should().Be("value2");
    }

    [Fact]
    void WhenReplaceInClassObjectThenDataIsReplaced()
    {
        var constraint = Parse("""
            {
                "type": "filterJsonContent",
                "actions": [{"type": "replace", "path": "$.name", "replacement": "Alice"}]
            }
            """);
        var handler = _provider.GetHandler(constraint);
        var original = new TestPerson { Name = "Bob", Age = 32 };

        var result = (JsonElement)handler(original);

        result.GetProperty("name").GetString().Should().Be("Alice");
        result.GetProperty("age").GetInt32().Should().Be(32);
    }

    [Fact]
    void WhenJsonElementArrayThenElementsAreFilteredIndividually()
    {
        var constraint = Parse("""
            {
                "type": "filterJsonContent",
                "actions": [{"type": "blacken", "path": "$.key1", "replacement": "X"}]
            }
            """);
        var handler = _provider.GetHandler(constraint);
        var original = Parse("""[{"key1": "value1", "key2": "v2"}, {"key1": "abc", "key2": "v3"}]""");

        var result = (JsonElement)handler(original);

        result.ValueKind.Should().Be(JsonValueKind.Array);
        var elements = result.EnumerateArray().ToList();
        elements.Should().HaveCount(2);
        elements[0].GetProperty("key1").GetString().Should().Be("XXXXXX");
        elements[0].GetProperty("key2").GetString().Should().Be("v2");
        elements[1].GetProperty("key1").GetString().Should().Be("XXX");
        elements[1].GetProperty("key2").GetString().Should().Be("v3");
    }

    [Fact]
    void WhenContentFilterAppliedThenOriginalNotMutated()
    {
        var constraint = Parse("""{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.sensitive"}]}""");
        var handler = _provider.GetHandler(constraint);
        var original = Parse("""{"sensitive": "secret", "public": "data"}""");

        handler(original);

        original.TryGetProperty("sensitive", out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("__proto__")]
    [InlineData("constructor")]
    [InlineData("prototype")]
    void WhenPathContainsPrototypePollutionSegmentThenRejected(string segment)
    {
        var constraint = Parse($$"""{"type": "filterJsonContent", "actions": [{"type": "delete", "path": "$.{{segment}}"}]}""");
        var handler = _provider.GetHandler(constraint);
        var original = Parse("""{"name": "Alice"}""");

        var act = () => handler(original);

        act.Should().Throw<AccessConstraintViolationException>();
    }

    [Fact]
    void WhenRegexPatternIsCatastrophicBacktrackingThenRejected()
    {
        var constraint = Parse("""
            {
                "type": "filterJsonContent",
                "actions": [{"type": "delete", "path": "$.key1"}],
                "conditions": [{"path": "$.key2", "type": "=~", "value": "(a+)+b"}]
            }
            """);

        var act = () => _provider.GetHandler(constraint);

        act.Should().Throw<AccessConstraintViolationException>()
            .WithMessage("*Unsafe regex*");
    }

    [Fact]
    void WhenHandlerHandlesListMultipleConditionsFirstHoldsSecondDoesNotThenNoModifications()
    {
        var constraint = Parse("""
            {
                "type": "filterJsonContent",
                "actions": [{"type": "delete", "path": "$.key1"}],
                "conditions": [
                    {"path": "$.key2", "type": "==", "value": "value2"},
                    {"path": "$.key3", "type": "==", "value": "other"}
                ]
            }
            """);
        var handler = _provider.GetHandler(constraint);
        var original = new List<object>
        {
            Parse("""{"key1": "value1", "key2": "value2", "key3": "value3"}"""),
            Parse("""{"key1": "value1", "key2": "value2", "key3": "value3"}""")
        };

        var result = (IList<object?>)handler(original);

        ((JsonElement)result[0]!).GetProperty("key1").GetString().Should().Be("value1");
        ((JsonElement)result[1]!).GetProperty("key1").GetString().Should().Be("value1");
    }

    [Fact]
    void WhenHandlerHandlesListNumberAndTextComparisonsThenModifies()
    {
        var constraint = Parse("""
            {
                "type": "filterJsonContent",
                "actions": [{"type": "delete", "path": "$.key1"}],
                "conditions": [
                    {"path": "$.key2", "type": "==", "value": 2},
                    {"path": "$.key3", "type": "!=", "value": "other"}
                ]
            }
            """);
        var handler = _provider.GetHandler(constraint);
        var original = new List<object>
        {
            Parse("""{"key1": "value1", "key2": 2, "key3": 3}"""),
            Parse("""{"key1": "value1", "key2": 2, "key3": 3}""")
        };

        var result = (IList<object?>)handler(original);

        ((JsonElement)result[0]!).TryGetProperty("key1", out _).Should().BeFalse();
        ((JsonElement)result[1]!).TryGetProperty("key1", out _).Should().BeFalse();
    }

    private class TestPerson
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }
}
