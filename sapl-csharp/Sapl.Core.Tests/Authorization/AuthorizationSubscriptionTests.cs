using System.Text.Json;
using FluentAssertions;
using Sapl.Core.Authorization;

namespace Sapl.Core.Tests.Authorization;

public class AuthorizationSubscriptionTests
{
    [Fact]
    void WhenCreatingWithObjectsThenSerializesCorrectly()
    {
        var sub = AuthorizationSubscription.Create("alice", "read", "document1");
        var json = sub.ToJsonString();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("subject").GetString().Should().Be("alice");
        doc.RootElement.GetProperty("action").GetString().Should().Be("read");
        doc.RootElement.GetProperty("resource").GetString().Should().Be("document1");
    }

    [Fact]
    void WhenCreatingWithComplexObjectsThenSerializesCorrectly()
    {
        var subject = new { name = "alice", role = "admin" };
        var sub = AuthorizationSubscription.Create(subject, "read", "document1");
        var json = sub.ToJsonString();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("subject").GetProperty("name").GetString().Should().Be("alice");
    }

    [Fact]
    void WhenEnvironmentProvidedThenIncludedInJson()
    {
        var sub = AuthorizationSubscription.Create("alice", "read", "doc", environment: "prod");
        var json = sub.ToJsonString();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("environment").GetString().Should().Be("prod");
    }

    [Fact]
    void WhenEnvironmentNullThenOmittedFromJson()
    {
        var sub = AuthorizationSubscription.Create("alice", "read", "doc");
        var json = sub.ToJsonString();
        json.Should().NotContain("environment");
    }

    [Fact]
    void WhenSecretsProvidedThenIncludedInJson()
    {
        var sub = AuthorizationSubscription.Create("alice", "read", "doc", secrets: new { apiKey = "secret123" });
        var json = sub.ToJsonString();
        json.Should().Contain("apiKey");
    }

    [Fact]
    void WhenSecretsProvidedThenExcludedFromLoggableString()
    {
        var sub = AuthorizationSubscription.Create("alice", "read", "doc", secrets: new { apiKey = "secret123" });
        var loggable = sub.ToLoggableString();
        loggable.Should().NotContain("secret123");
        loggable.Should().NotContain("secrets");
    }

    [Fact]
    void WhenCreatingWithJsonElementsThenPreservesValues()
    {
        var subjectElement = JsonDocument.Parse("42").RootElement.Clone();
        var actionElement = JsonDocument.Parse("\"read\"").RootElement.Clone();
        var resourceElement = JsonDocument.Parse("{\"type\":\"file\"}").RootElement.Clone();

        var sub = new AuthorizationSubscription
        {
            Subject = subjectElement,
            Action = actionElement,
            Resource = resourceElement,
        };

        sub.Subject.GetInt32().Should().Be(42);
        sub.Action.GetString().Should().Be("read");
        sub.Resource.GetProperty("type").GetString().Should().Be("file");
    }
}
