using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sapl.Core.Constraints.Providers;

namespace Sapl.Core.Tests.Constraints;

public class ContentFilteringProviderTests
{
    private readonly ContentFilteringProvider _provider = new(
        Substitute.For<ILogger<ContentFilteringProvider>>());

    [Fact]
    void WhenTypeIsFilterJsonContentThenIsResponsible()
    {
        var constraint = Parse("""{"type":"filterJsonContent","actions":[]}""");
        _provider.IsResponsible(constraint).Should().BeTrue();
    }

    [Fact]
    void WhenTypeIsOtherThenNotResponsible()
    {
        var constraint = Parse("""{"type":"other"}""");
        _provider.IsResponsible(constraint).Should().BeFalse();
    }

    [Fact]
    void WhenBlackenActionThenBlackensField()
    {
        var constraint = Parse("""
            {
                "type":"filterJsonContent",
                "actions":[{"type":"blacken","path":"$.ssn","discloseRight":4}]
            }
            """);
        var input = Parse("""{"name":"Jane","ssn":"123-45-6789"}""");

        var handler = _provider.GetHandler(constraint);
        var result = (JsonElement)handler(input);

        result.GetProperty("ssn").GetString().Should().Be("XXXXXXX6789");
        result.GetProperty("name").GetString().Should().Be("Jane");
    }

    [Fact]
    void WhenDeleteActionThenRemovesField()
    {
        var constraint = Parse("""
            {
                "type":"filterJsonContent",
                "actions":[{"type":"delete","path":"$.internalNotes"}]
            }
            """);
        var input = Parse("""{"name":"Jane","internalNotes":"secret"}""");

        var handler = _provider.GetHandler(constraint);
        var result = (JsonElement)handler(input);

        result.TryGetProperty("internalNotes", out _).Should().BeFalse();
        result.GetProperty("name").GetString().Should().Be("Jane");
    }

    [Fact]
    void WhenReplaceActionThenReplacesField()
    {
        var constraint = Parse("""
            {
                "type":"filterJsonContent",
                "actions":[{"type":"replace","path":"$.email","replacement":"redacted@example.com"}]
            }
            """);
        var input = Parse("""{"name":"Jane","email":"jane@real.com"}""");

        var handler = _provider.GetHandler(constraint);
        var result = (JsonElement)handler(input);

        result.GetProperty("email").GetString().Should().Be("redacted@example.com");
    }

    [Fact]
    void WhenMultipleActionsThenAllApplied()
    {
        var constraint = Parse("""
            {
                "type":"filterJsonContent",
                "actions":[
                    {"type":"blacken","path":"$.ssn","discloseRight":4},
                    {"type":"delete","path":"$.internalNotes"},
                    {"type":"replace","path":"$.email","replacement":"redacted@example.com"}
                ]
            }
            """);
        var input = Parse("""{"ssn":"123-45-6789","internalNotes":"secret","email":"jane@real.com"}""");

        var handler = _provider.GetHandler(constraint);
        var result = (JsonElement)handler(input);

        result.GetProperty("ssn").GetString().Should().Be("XXXXXXX6789");
        result.TryGetProperty("internalNotes", out _).Should().BeFalse();
        result.GetProperty("email").GetString().Should().Be("redacted@example.com");
    }

    [Fact]
    void WhenBlackenWithDiscloseLeftThenPreservesLeft()
    {
        var constraint = Parse("""
            {
                "type":"filterJsonContent",
                "actions":[{"type":"blacken","path":"$.phone","discloseLeft":3}]
            }
            """);
        var input = Parse("""{"phone":"555-1234"}""");

        var handler = _provider.GetHandler(constraint);
        var result = (JsonElement)handler(input);

        result.GetProperty("phone").GetString().Should().Be("555XXXXX");
    }

    [Fact]
    void WhenPrototypePollutionPathThenRejected()
    {
        var constraint = Parse("""
            {
                "type":"filterJsonContent",
                "actions":[{"type":"blacken","path":"$.__proto__.polluted"}]
            }
            """);
        var input = Parse("""{"name":"safe"}""");

        var handler = _provider.GetHandler(constraint);
        var result = (JsonElement)handler(input);

        result.GetProperty("name").GetString().Should().Be("safe");
    }

    [Fact]
    void WhenFieldDoesNotExistThenNoError()
    {
        var constraint = Parse("""
            {
                "type":"filterJsonContent",
                "actions":[{"type":"delete","path":"$.nonExistent"}]
            }
            """);
        var input = Parse("""{"name":"Jane"}""");

        var handler = _provider.GetHandler(constraint);
        var act = () => handler(input);

        act.Should().NotThrow();
    }

    [Fact]
    void WhenInputIsArrayThenAppliesActionToEachElement()
    {
        var constraint = Parse("""
            {
                "type":"filterJsonContent",
                "actions":[{"type":"blacken","path":"$.ssn","discloseRight":4}]
            }
            """);
        var input = Parse("""[{"name":"Jane","ssn":"123-45-6789"},{"name":"John","ssn":"987-65-4321"}]""");

        var handler = _provider.GetHandler(constraint);
        var result = (JsonElement)handler(input);

        result.GetArrayLength().Should().Be(2);
        result[0].GetProperty("ssn").GetString().Should().Be("XXXXXXX6789");
        result[1].GetProperty("ssn").GetString().Should().Be("XXXXXXX4321");
        result[0].GetProperty("name").GetString().Should().Be("Jane");
    }

    [Fact]
    void WhenInputIsArrayAndDeleteActionThenRemovesFromEachElement()
    {
        var constraint = Parse("""
            {
                "type":"filterJsonContent",
                "actions":[{"type":"delete","path":"$.secret"}]
            }
            """);
        var input = Parse("""[{"name":"Jane","secret":"a"},{"name":"John","secret":"b"}]""");

        var handler = _provider.GetHandler(constraint);
        var result = (JsonElement)handler(input);

        result.GetArrayLength().Should().Be(2);
        result[0].TryGetProperty("secret", out _).Should().BeFalse();
        result[1].TryGetProperty("secret", out _).Should().BeFalse();
    }

    [Fact]
    void WhenOperatesOnDeepCloneThenOriginalUnchanged()
    {
        var constraint = Parse("""
            {
                "type":"filterJsonContent",
                "actions":[{"type":"replace","path":"$.name","replacement":"changed"}]
            }
            """);
        var input = Parse("""{"name":"original"}""");

        var handler = _provider.GetHandler(constraint);
        handler(input);

        input.GetProperty("name").GetString().Should().Be("original");
    }

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
