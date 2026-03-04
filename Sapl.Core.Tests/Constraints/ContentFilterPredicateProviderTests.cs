using System.Text.Json;
using FluentAssertions;
using Sapl.Core.Constraints.Providers;

namespace Sapl.Core.Tests.Constraints;

public class ContentFilterPredicateProviderTests
{
    private readonly ContentFilterPredicateProvider _provider = new();

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
        yield return ["correct type", Parse("""{"type": "jsonContentFilterPredicate"}"""), true];
    }

    [Fact]
    void WhenPredicateNotMatchingThenFalse()
    {
        var constraint = Parse("""
            {
                "type": "jsonContentFilterPredicate",
                "conditions": [{"path": "$.key1", "type": "==", "value": "another value that does not match"}]
            }
            """);
        var handler = _provider.GetHandler(constraint);
        var original = Parse("""{"key1": "value1", "key2": "value2"}""");

        handler(original).Should().BeFalse();
    }

    [Fact]
    void WhenPredicateMatchingThenTrue()
    {
        var constraint = Parse("""
            {
                "type": "jsonContentFilterPredicate",
                "conditions": [{"path": "$.key1", "type": "==", "value": "value1"}]
            }
            """);
        var handler = _provider.GetHandler(constraint);
        var original = Parse("""{"key1": "value1", "key2": "value2"}""");

        handler(original).Should().BeTrue();
    }

    [Fact]
    void WhenNoConditionsThenAlwaysTrue()
    {
        var constraint = Parse("""{"type": "jsonContentFilterPredicate"}""");
        var handler = _provider.GetHandler(constraint);
        var original = Parse("""{"key1": "value1"}""");

        handler(original).Should().BeTrue();
    }
}
