using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Sapl.AspNetCore.Subscription;

namespace Sapl.AspNetCore.Tests.Subscription;

public class SubscriptionBuilderTests
{
    [Fact]
    void WhenStaticValuesThenUsesStaticValues()
    {
        var builder = new SubscriptionBuilder()
            .WithStaticSubject("alice")
            .WithStaticAction("read")
            .WithStaticResource("document");

        var context = new SubscriptionContext();
        var sub = builder.Build(context);

        sub.Subject.GetString().Should().Be("alice");
        sub.Action.GetString().Should().Be("read");
        sub.Resource.GetString().Should().Be("document");
    }

    [Fact]
    void WhenCallbacksThenUsesCallbackValues()
    {
        var builder = new SubscriptionBuilder()
            .WithSubject(_ => "dynamic-subject")
            .WithAction(_ => "dynamic-action")
            .WithResource(_ => "dynamic-resource");

        var context = new SubscriptionContext();
        var sub = builder.Build(context);

        sub.Subject.GetString().Should().Be("dynamic-subject");
    }

    [Fact]
    void WhenCallbackOverridesStaticThenCallbackWins()
    {
        var builder = new SubscriptionBuilder()
            .WithStaticSubject("static")
            .WithSubject(_ => "callback");

        var context = new SubscriptionContext();
        var sub = builder.Build(context);

        sub.Subject.GetString().Should().Be("callback");
    }

    [Fact]
    void WhenNoSubjectAndNoAuthThenAnonymous()
    {
        var builder = new SubscriptionBuilder()
            .WithStaticAction("read")
            .WithStaticResource("doc");

        var context = new SubscriptionContext();
        var sub = builder.Build(context);

        sub.Subject.GetString().Should().Be("anonymous");
    }

    [Fact]
    void WhenSecretsThenIncludedInSubscription()
    {
        var builder = new SubscriptionBuilder()
            .WithStaticSubject("alice")
            .WithStaticAction("read")
            .WithStaticResource("doc")
            .WithSecrets(_ => new { token = "secret123" });

        var context = new SubscriptionContext();
        var sub = builder.Build(context);

        sub.Secrets.Should().NotBeNull();
        sub.ToLoggableString().Should().NotContain("secret123");
    }

    [Fact]
    void WhenReturnValueInContextThenAvailableToCallbacks()
    {
        var builder = new SubscriptionBuilder()
            .WithStaticSubject("alice")
            .WithStaticAction("read")
            .WithResource(ctx => new { data = ctx.ReturnValue });

        var context = new SubscriptionContext { ReturnValue = "my-return-value" };
        var sub = builder.Build(context);

        sub.Resource.GetProperty("data").GetString().Should().Be("my-return-value");
    }

    [Fact]
    void WhenFromPreEnforceAttributeThenUsesAttributeValues()
    {
        var attr = new Sapl.AspNetCore.Attributes.PreEnforceAttribute
        {
            Action = "readPatient",
            Resource = "patient",
        };

        var builder = SubscriptionBuilder.FromAttribute(attr);
        var context = new SubscriptionContext();
        var sub = builder.Build(context);

        sub.Action.GetString().Should().Be("readPatient");
        sub.Resource.GetString().Should().Be("patient");
    }
}
