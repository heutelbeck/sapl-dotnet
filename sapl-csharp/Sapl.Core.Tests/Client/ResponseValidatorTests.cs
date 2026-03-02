using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sapl.Core.Authorization;
using Sapl.Core.Client;

namespace Sapl.Core.Tests.Client;

public class ResponseValidatorTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();

    [Theory]
    [InlineData("{\"decision\":\"PERMIT\"}", Decision.Permit)]
    [InlineData("{\"decision\":\"DENY\"}", Decision.Deny)]
    [InlineData("{\"decision\":\"INDETERMINATE\"}", Decision.Indeterminate)]
    [InlineData("{\"decision\":\"NOT_APPLICABLE\"}", Decision.NotApplicable)]
    void WhenValidDecisionThenParsesCorrectly(string json, Decision expected)
    {
        var result = ResponseValidator.ParseDecisionFromJson(json, _logger);
        result.Decision.Should().Be(expected);
    }

    [Fact]
    void WhenDecisionWithObligationsThenParsesObligations()
    {
        var json = """{"decision":"PERMIT","obligations":[{"type":"log"},{"type":"audit"}]}""";
        var result = ResponseValidator.ParseDecisionFromJson(json, _logger);

        result.Decision.Should().Be(Decision.Permit);
        result.Obligations.Should().HaveCount(2);
    }

    [Fact]
    void WhenDecisionWithAdviceThenParsesAdvice()
    {
        var json = """{"decision":"PERMIT","advice":[{"type":"info"}]}""";
        var result = ResponseValidator.ParseDecisionFromJson(json, _logger);

        result.Decision.Should().Be(Decision.Permit);
        result.Advice.Should().HaveCount(1);
    }

    [Fact]
    void WhenDecisionWithResourceThenParsesResource()
    {
        var json = """{"decision":"PERMIT","resource":{"id":42,"name":"replaced"}}""";
        var result = ResponseValidator.ParseDecisionFromJson(json, _logger);

        result.Decision.Should().Be(Decision.Permit);
        result.HasResource.Should().BeTrue();
        result.Resource!.Value.GetProperty("id").GetInt32().Should().Be(42);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("\"string\"")]
    [InlineData("[1,2,3]")]
    void WhenNotObjectThenReturnsIndeterminate(string json)
    {
        var result = ResponseValidator.ParseDecisionFromJson(json, _logger);
        result.Decision.Should().Be(Decision.Indeterminate);
    }

    [Fact]
    void WhenMissingDecisionFieldThenReturnsIndeterminate()
    {
        var json = """{"obligations":[]}""";
        var result = ResponseValidator.ParseDecisionFromJson(json, _logger);
        result.Decision.Should().Be(Decision.Indeterminate);
    }

    [Fact]
    void WhenInvalidDecisionValueThenReturnsIndeterminate()
    {
        var json = """{"decision":"UNKNOWN"}""";
        var result = ResponseValidator.ParseDecisionFromJson(json, _logger);
        result.Decision.Should().Be(Decision.Indeterminate);
    }

    [Fact]
    void WhenDecisionFieldIsNumberThenReturnsIndeterminate()
    {
        var json = """{"decision":42}""";
        var result = ResponseValidator.ParseDecisionFromJson(json, _logger);
        result.Decision.Should().Be(Decision.Indeterminate);
    }

    [Fact]
    void WhenObligationsNotArrayThenIgnored()
    {
        var json = """{"decision":"PERMIT","obligations":"not-an-array"}""";
        var result = ResponseValidator.ParseDecisionFromJson(json, _logger);

        result.Decision.Should().Be(Decision.Permit);
        result.Obligations.Should().BeNull();
    }

    [Fact]
    void WhenAdviceNotArrayThenIgnored()
    {
        var json = """{"decision":"PERMIT","advice":"not-an-array"}""";
        var result = ResponseValidator.ParseDecisionFromJson(json, _logger);

        result.Decision.Should().Be(Decision.Permit);
        result.Advice.Should().BeNull();
    }

    [Fact]
    void WhenExtraFieldsThenSilentlyDropped()
    {
        var json = """{"decision":"PERMIT","unknown_field":"value","another":123}""";
        var result = ResponseValidator.ParseDecisionFromJson(json, _logger);
        result.Decision.Should().Be(Decision.Permit);
    }

    [Fact]
    void WhenInvalidJsonThenReturnsIndeterminate()
    {
        var result = ResponseValidator.ParseDecisionFromJson("{broken json", _logger);
        result.Decision.Should().Be(Decision.Indeterminate);
    }

    [Fact]
    void WhenIdentifiableDecisionValidThenParsesCorrectly()
    {
        var json = """
            {
                "authorizationSubscriptionId":"sub-1",
                "authorizationDecision":{"decision":"PERMIT"}
            }
            """;
        var result = ResponseValidator.ParseIdentifiableDecisionFromJson(json, _logger);

        result.Should().NotBeNull();
        result!.SubscriptionId.Should().Be("sub-1");
        result.Decision.Decision.Should().Be(Decision.Permit);
    }

    [Fact]
    void WhenIdentifiableDecisionMissingIdThenReturnsNull()
    {
        var json = """{"authorizationDecision":{"decision":"PERMIT"}}""";
        var result = ResponseValidator.ParseIdentifiableDecisionFromJson(json, _logger);
        result.Should().BeNull();
    }

    [Fact]
    void WhenIdentifiableDecisionMissingDecisionThenReturnsNull()
    {
        var json = """{"authorizationSubscriptionId":"sub-1"}""";
        var result = ResponseValidator.ParseIdentifiableDecisionFromJson(json, _logger);
        result.Should().BeNull();
    }

    [Fact]
    void WhenMultiDecisionValidThenParsesCorrectly()
    {
        var json = """
            {
                "decisions":{
                    "sub-1":{"decision":"PERMIT"},
                    "sub-2":{"decision":"DENY"}
                }
            }
            """;
        var result = ResponseValidator.ParseMultiDecisionFromJson(json, _logger);

        result.Should().NotBeNull();
        result!.Decisions.Should().HaveCount(2);
        result.Decisions["sub-1"].Decision.Should().Be(Decision.Permit);
        result.Decisions["sub-2"].Decision.Should().Be(Decision.Deny);
    }

    [Fact]
    void WhenMultiDecisionMissingDecisionsFieldThenReturnsNull()
    {
        var json = """{"other":"field"}""";
        var result = ResponseValidator.ParseMultiDecisionFromJson(json, _logger);
        result.Should().BeNull();
    }

    [Fact]
    void WhenMultiDecisionWithInvalidSubDecisionThenSubReturnsIndeterminate()
    {
        var json = """
            {
                "decisions":{
                    "sub-1":{"decision":"PERMIT"},
                    "sub-2":{"decision":"INVALID"}
                }
            }
            """;
        var result = ResponseValidator.ParseMultiDecisionFromJson(json, _logger);

        result.Should().NotBeNull();
        result!.Decisions["sub-1"].Decision.Should().Be(Decision.Permit);
        result.Decisions["sub-2"].Decision.Should().Be(Decision.Indeterminate);
    }
}
