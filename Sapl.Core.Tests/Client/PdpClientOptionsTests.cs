using FluentAssertions;
using Sapl.Core.Client;

namespace Sapl.Core.Tests.Client;

public class PdpClientOptionsTests
{
    [Fact]
    void WhenValidHttpsUrlThenValidationPasses()
    {
        var options = new PdpClientOptions { BaseUrl = "https://localhost:8443" };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    void WhenEmptyBaseUrlThenThrows()
    {
        var options = new PdpClientOptions { BaseUrl = "" };
        var act = () => options.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*" + PdpClientOptions.ErrorBaseUrlEmpty + "*");
    }

    [Fact]
    void WhenInvalidUrlThenThrows()
    {
        var options = new PdpClientOptions { BaseUrl = "not-a-url" };
        var act = () => options.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*" + PdpClientOptions.ErrorBaseUrlInvalid + "*");
    }

    [Fact]
    void WhenHttpWithoutFlagThenThrows()
    {
        var options = new PdpClientOptions { BaseUrl = "http://localhost:8443" };
        var act = () => options.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*" + PdpClientOptions.ErrorInsecureHttp + "*");
    }

    [Fact]
    void WhenHttpWithFlagThenPasses()
    {
        var options = new PdpClientOptions
        {
            BaseUrl = "http://localhost:8443",
            AllowInsecureConnections = true,
        };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    void WhenBothTokenAndUsernameThenThrows()
    {
        var options = new PdpClientOptions
        {
            BaseUrl = "https://localhost:8443",
            Token = "my-token",
            Username = "admin",
            Secret = "password",
        };
        var act = () => options.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*" + PdpClientOptions.ErrorAuthDualConfig + "*");
    }

    [Fact]
    void WhenUsernameWithoutSecretThenThrows()
    {
        var options = new PdpClientOptions
        {
            BaseUrl = "https://localhost:8443",
            Username = "admin",
        };
        var act = () => options.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*" + PdpClientOptions.ErrorAuthBasicIncomplete + "*");
    }

    [Fact]
    void WhenSecretWithoutUsernameThenThrows()
    {
        var options = new PdpClientOptions
        {
            BaseUrl = "https://localhost:8443",
            Secret = "password",
        };
        var act = () => options.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*" + PdpClientOptions.ErrorAuthBasicIncomplete + "*");
    }

    [Fact]
    void WhenBearerTokenOnlyThenPasses()
    {
        var options = new PdpClientOptions
        {
            BaseUrl = "https://localhost:8443",
            Token = "my-token",
        };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    void WhenBasicAuthOnlyThenPasses()
    {
        var options = new PdpClientOptions
        {
            BaseUrl = "https://localhost:8443",
            Username = "admin",
            Secret = "password",
        };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    void WhenDefaultsThenReasonableValues()
    {
        var options = new PdpClientOptions { BaseUrl = "https://localhost:8443" };
        options.TimeoutMs.Should().Be(5000);
        options.StreamingRetryBaseDelayMs.Should().Be(1000);
        options.StreamingRetryMaxDelayMs.Should().Be(30000);
        options.StreamingMaxRetries.Should().Be(0);
        options.AllowInsecureConnections.Should().BeFalse();
    }
}
