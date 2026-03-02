using System.Text.Json;
using FluentAssertions;
using Sapl.Core.Authorization;

namespace Sapl.Core.Tests.Authorization;

public class DecisionTests
{
    [Theory]
    [InlineData(Decision.Permit, "\"PERMIT\"")]
    [InlineData(Decision.Deny, "\"DENY\"")]
    [InlineData(Decision.Indeterminate, "\"INDETERMINATE\"")]
    [InlineData(Decision.NotApplicable, "\"NOT_APPLICABLE\"")]
    void WhenSerializingThenProducesUppercaseString(Decision decision, string expected)
    {
        var json = JsonSerializer.Serialize(decision);
        json.Should().Be(expected);
    }

    [Theory]
    [InlineData("\"PERMIT\"", Decision.Permit)]
    [InlineData("\"DENY\"", Decision.Deny)]
    [InlineData("\"INDETERMINATE\"", Decision.Indeterminate)]
    [InlineData("\"NOT_APPLICABLE\"", Decision.NotApplicable)]
    void WhenDeserializingThenParsesUppercaseString(string json, Decision expected)
    {
        var decision = JsonSerializer.Deserialize<Decision>(json);
        decision.Should().Be(expected);
    }
}
