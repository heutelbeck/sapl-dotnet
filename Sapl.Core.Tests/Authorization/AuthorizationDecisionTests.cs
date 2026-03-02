using System.Text.Json;
using FluentAssertions;
using Sapl.Core.Authorization;

namespace Sapl.Core.Tests.Authorization;

public class AuthorizationDecisionTests
{
    [Fact]
    void WhenComparingStaticInstancesThenEqual()
    {
        var a = AuthorizationDecision.PermitInstance;
        var b = new AuthorizationDecision { Decision = Decision.Permit };
        a.Should().Be(b);
    }

    [Fact]
    void WhenDecisionsDifferThenNotEqual()
    {
        AuthorizationDecision.PermitInstance.Should()
            .NotBe(AuthorizationDecision.DenyInstance);
    }

    [Fact]
    void WhenObligationsMatchThenEqual()
    {
        var obligations = ParseArray("[{\"type\":\"log\"},{\"type\":\"audit\"}]");
        var a = new AuthorizationDecision { Decision = Decision.Permit, Obligations = obligations };
        var b = new AuthorizationDecision { Decision = Decision.Permit, Obligations = obligations };
        a.Should().Be(b);
    }

    [Fact]
    void WhenObligationsDifferThenNotEqual()
    {
        var a = new AuthorizationDecision
        {
            Decision = Decision.Permit,
            Obligations = ParseArray("[{\"type\":\"log\"}]"),
        };
        var b = new AuthorizationDecision
        {
            Decision = Decision.Permit,
            Obligations = ParseArray("[{\"type\":\"audit\"}]"),
        };
        a.Should().NotBe(b);
    }

    [Fact]
    void WhenOneHasObligationsOtherDoesNotThenNotEqual()
    {
        var a = new AuthorizationDecision
        {
            Decision = Decision.Permit,
            Obligations = ParseArray("[{\"type\":\"log\"}]"),
        };
        var b = new AuthorizationDecision { Decision = Decision.Permit };
        a.Should().NotBe(b);
    }

    [Fact]
    void WhenAdviceMatchesThenEqual()
    {
        var advice = ParseArray("[{\"type\":\"info\"}]");
        var a = new AuthorizationDecision { Decision = Decision.Permit, Advice = advice };
        var b = new AuthorizationDecision { Decision = Decision.Permit, Advice = advice };
        a.Should().Be(b);
    }

    [Fact]
    void WhenResourceMatchesThenEqual()
    {
        var resource = JsonDocument.Parse("{\"id\":1}").RootElement.Clone();
        var a = new AuthorizationDecision { Decision = Decision.Permit, Resource = resource };
        var b = new AuthorizationDecision { Decision = Decision.Permit, Resource = resource };
        a.Should().Be(b);
    }

    [Fact]
    void WhenResourcesDifferThenNotEqual()
    {
        var a = new AuthorizationDecision
        {
            Decision = Decision.Permit,
            Resource = JsonDocument.Parse("{\"id\":1}").RootElement.Clone(),
        };
        var b = new AuthorizationDecision
        {
            Decision = Decision.Permit,
            Resource = JsonDocument.Parse("{\"id\":2}").RootElement.Clone(),
        };
        a.Should().NotBe(b);
    }

    [Fact]
    void WhenHasResourceThenHasResourceIsTrue()
    {
        var decision = new AuthorizationDecision
        {
            Decision = Decision.Permit,
            Resource = JsonDocument.Parse("42").RootElement.Clone(),
        };
        decision.HasResource.Should().BeTrue();
    }

    [Fact]
    void WhenNoResourceThenHasResourceIsFalse()
    {
        AuthorizationDecision.PermitInstance.HasResource.Should().BeFalse();
    }

    [Fact]
    void WhenSerializingThenProducesValidJson()
    {
        var decision = new AuthorizationDecision
        {
            Decision = Decision.Permit,
            Obligations = ParseArray("[{\"type\":\"log\"}]"),
        };
        var json = JsonSerializer.Serialize(decision, SerializerDefaults.Options);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("decision").GetString().Should().Be("PERMIT");
        doc.RootElement.GetProperty("obligations").GetArrayLength().Should().Be(1);
    }

    [Fact]
    void WhenDeserializingThenParsesCorrectly()
    {
        var json = """{"decision":"DENY","advice":[{"type":"info"}],"resource":{"id":42}}""";
        var decision = JsonSerializer.Deserialize<AuthorizationDecision>(json, SerializerDefaults.Options);
        decision.Should().NotBeNull();
        decision!.Decision.Should().Be(Decision.Deny);
        decision.Advice.Should().HaveCount(1);
        decision.HasResource.Should().BeTrue();
    }

    [Fact]
    void WhenEqualDecisionsThenHashCodesMatch()
    {
        var obligations = ParseArray("[{\"type\":\"log\"}]");
        var a = new AuthorizationDecision { Decision = Decision.Permit, Obligations = obligations };
        var b = new AuthorizationDecision { Decision = Decision.Permit, Obligations = obligations };
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    void WhenDeepNestedJsonMatchesThenEqual()
    {
        var nested = """[{"a":{"b":{"c":[1,2,3]}}}]""";
        var a = new AuthorizationDecision
        {
            Decision = Decision.Permit,
            Obligations = ParseArray(nested),
        };
        var b = new AuthorizationDecision
        {
            Decision = Decision.Permit,
            Obligations = ParseArray(nested),
        };
        a.Should().Be(b);
    }

    [Fact]
    void WhenNullObligationsSerializedThenFieldOmitted()
    {
        var json = JsonSerializer.Serialize(AuthorizationDecision.PermitInstance, SerializerDefaults.Options);
        json.Should().NotContain("obligations");
    }

    private static IReadOnlyList<JsonElement> ParseArray(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }
}
