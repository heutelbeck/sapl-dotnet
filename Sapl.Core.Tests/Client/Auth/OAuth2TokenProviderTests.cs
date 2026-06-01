using System.Net;
using System.Text;
using FluentAssertions;
using Sapl.Core.Client.Auth;
using Xunit;

namespace Sapl.Core.Tests.Client.Auth;

public sealed class OAuth2TokenProviderTests
{
    private const string Discovery = """{"token_endpoint":"http://issuer.test/token"}""";

    private static OAuth2TokenProvider Provider(StubHandler handler, TimeSpan? refreshGuard = null) =>
        new(
            new OAuth2TokenProviderOptions
            {
                IssuerUrl = "http://issuer.test",
                ClientId = "client",
                ClientSecret = "secret",
                RefreshGuard = refreshGuard ?? TimeSpan.FromSeconds(30),
            },
            new HttpClient(handler));

    [Fact]
    async Task WhenCalledTwiceWithinLifetimeThenReusesCachedTokenWithoutSecondGrant()
    {
        var handler = new StubHandler(Discovery, """{"access_token":"abc","expires_in":3600}""");
        var provider = Provider(handler);

        var first = await provider.GetAccessTokenAsync();
        var second = await provider.GetAccessTokenAsync();

        first.Should().Be("abc");
        second.Should().Be("abc");
        handler.TokenRequests.Should().Be(1);
    }

    [Fact]
    async Task WhenInvalidatedThenNextCallAcquiresAFreshToken()
    {
        var handler = new StubHandler(Discovery, """{"access_token":"abc","expires_in":3600}""");
        var provider = Provider(handler);

        await provider.GetAccessTokenAsync();
        provider.Invalidate();
        await provider.GetAccessTokenAsync();

        handler.TokenRequests.Should().Be(2);
    }

    [Fact]
    async Task WhenCachedTokenIsWithinTheRefreshGuardThenItIsRefreshed()
    {
        var handler = new StubHandler(Discovery, """{"access_token":"abc","expires_in":5}""");
        var provider = Provider(handler, TimeSpan.FromSeconds(30));

        await provider.GetAccessTokenAsync();
        await provider.GetAccessTokenAsync();

        handler.TokenRequests.Should().Be(2);
    }

    [Fact]
    async Task WhenGrantResponseHasNoAccessTokenThenThrows()
    {
        var handler = new StubHandler(Discovery, """{"expires_in":3600}""");
        var provider = Provider(handler);

        var act = async () => await provider.GetAccessTokenAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(OAuth2TokenProvider.ErrorMissingAccessToken);
    }

    [Fact]
    async Task WhenDiscoveryHasNoTokenEndpointThenThrows()
    {
        var handler = new StubHandler("""{"issuer":"http://issuer.test"}""", """{"access_token":"abc"}""");
        var provider = Provider(handler);

        var act = async () => await provider.GetAccessTokenAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(OAuth2TokenProvider.ErrorMissingTokenEndpoint);
    }

    private sealed class StubHandler(string discoveryBody, string tokenBody) : HttpMessageHandler
    {
        public int TokenRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = discoveryBody;
            if (request.Method == HttpMethod.Post)
            {
                TokenRequests++;
                body = tokenBody;
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
