using FluentAssertions;

namespace Sapl.Rsocket.Tests;

public sealed class RsocketPdpClientOptionsTests
{
    private static RsocketPdpClientOptions Base() => new() { Host = "localhost", Port = 7000 };

    [Fact]
    void WhenLoopbackPlaintextNoAuthThenValidates()
    {
        var act = () => Base().Validate();

        act.Should().NotThrow();
    }

    [Fact]
    void WhenMultipleAuthSourcesThenThrows()
    {
        var options = Base() with { Token = "t", Username = "u", Secret = "s" };

        var act = () => options.Validate();

        act.Should().Throw<ArgumentException>().WithMessage("*" + RsocketPdpClientOptions.ErrorAuthConflict + "*");
    }

    [Fact]
    void WhenBasicAuthMissingSecretThenThrows()
    {
        var options = Base() with { Username = "u" };

        var act = () => options.Validate();

        act.Should().Throw<ArgumentException>().WithMessage("*" + RsocketPdpClientOptions.ErrorBasicIncomplete + "*");
    }

    [Fact]
    void WhenPlaintextToNonLoopbackHostThenThrows()
    {
        var options = Base() with { Host = "pdp.example.com" };

        var act = () => options.Validate();

        act.Should().Throw<ArgumentException>().WithMessage("*" + RsocketPdpClientOptions.ErrorPlaintextNonLoopback + "*");
    }

    [Fact]
    void WhenTlsToNonLoopbackHostThenValidates()
    {
        var options = Base() with { Host = "pdp.example.com", Tls = new RsocketTlsOptions { CaPemPath = "ca.pem" } };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }
}

public sealed class RsocketAuthTests
{
    [Fact]
    void SimpleEncodesTypeLengthUsernamePassword()
    {
        var metadata = RsocketAuth.Simple("ab", "cde");

        metadata.Should().Equal(0x80, 0x00, 0x02, (byte)'a', (byte)'b', (byte)'c', (byte)'d', (byte)'e');
    }

    [Fact]
    void BearerEncodesTypeThenToken()
    {
        var metadata = RsocketAuth.Bearer("xy");

        metadata.Should().Equal(0x81, (byte)'x', (byte)'y');
    }
}
