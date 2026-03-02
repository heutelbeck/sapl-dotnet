using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sapl.Core.Authorization;
using Sapl.Core.Client;

namespace Sapl.Core.Tests.Client;

public class PdpClientTests : IDisposable
{
    private readonly ILogger<PdpClient> _logger = Substitute.For<ILogger<PdpClient>>();
    private readonly MockHttpMessageHandler _handler = new();
    private readonly IHttpClientFactory _factory;
    private bool _disposed;

    public PdpClientTests()
    {
        _factory = Substitute.For<IHttpClientFactory>();
        _factory.CreateClient("SaplPdp")
            .Returns(_ => new HttpClient(_handler));
    }

    [Fact]
    async Task WhenDecideOncePermitThenReturnsPermit()
    {
        _handler.ResponseBody = """{"decision":"PERMIT"}""";
        var client = CreateClient();
        var sub = AuthorizationSubscription.Create("alice", "read", "doc");

        var result = await client.DecideOnceAsync(sub);

        result.Decision.Should().Be(Decision.Permit);
    }

    [Fact]
    async Task WhenDecideOnceDenyThenReturnsDeny()
    {
        _handler.ResponseBody = """{"decision":"DENY"}""";
        var client = CreateClient();
        var sub = AuthorizationSubscription.Create("alice", "write", "doc");

        var result = await client.DecideOnceAsync(sub);

        result.Decision.Should().Be(Decision.Deny);
    }

    [Fact]
    async Task WhenDecideOnceWithObligationsThenParsesObligations()
    {
        _handler.ResponseBody = """{"decision":"PERMIT","obligations":[{"type":"log"}]}""";
        var client = CreateClient();
        var sub = AuthorizationSubscription.Create("alice", "read", "doc");

        var result = await client.DecideOnceAsync(sub);

        result.Decision.Should().Be(Decision.Permit);
        result.Obligations.Should().HaveCount(1);
    }

    [Fact]
    async Task WhenDecideOnceServerErrorThenReturnsIndeterminate()
    {
        _handler.StatusCode = HttpStatusCode.InternalServerError;
        _handler.ResponseBody = "Internal Server Error";
        var client = CreateClient();
        var sub = AuthorizationSubscription.Create("alice", "read", "doc");

        var result = await client.DecideOnceAsync(sub);

        result.Decision.Should().Be(Decision.Indeterminate);
    }

    [Fact]
    async Task WhenDecideOnceUnauthorizedThenReturnsIndeterminate()
    {
        _handler.StatusCode = HttpStatusCode.Unauthorized;
        _handler.ResponseBody = "Unauthorized";
        var client = CreateClient();
        var sub = AuthorizationSubscription.Create("alice", "read", "doc");

        var result = await client.DecideOnceAsync(sub);

        result.Decision.Should().Be(Decision.Indeterminate);
    }

    [Fact]
    async Task WhenDecideOnceTimeoutThenReturnsIndeterminate()
    {
        _handler.Delay = TimeSpan.FromSeconds(10);
        var client = CreateClient(timeoutMs: 100);
        var sub = AuthorizationSubscription.Create("alice", "read", "doc");

        var result = await client.DecideOnceAsync(sub);

        result.Decision.Should().Be(Decision.Indeterminate);
    }

    [Fact]
    async Task WhenDecideOnceInvalidJsonThenReturnsIndeterminate()
    {
        _handler.ResponseBody = "not json at all";
        var client = CreateClient();
        var sub = AuthorizationSubscription.Create("alice", "read", "doc");

        var result = await client.DecideOnceAsync(sub);

        result.Decision.Should().Be(Decision.Indeterminate);
    }

    [Fact]
    async Task WhenDecideStreamingThenYieldsDecisions()
    {
        _handler.ResponseBody = "data: {\"decision\":\"PERMIT\"}\n\ndata: {\"decision\":\"DENY\"}\n\n";
        _handler.ResponseContentType = "text/event-stream";
        _handler.UseStream = true;
        var client = CreateClient();
        var sub = AuthorizationSubscription.Create("alice", "read", "doc");

        var decisions = new List<AuthorizationDecision>();
        await foreach (var d in client.Decide(sub))
        {
            decisions.Add(d);
            if (decisions.Count >= 2)
                break;
        }

        decisions.Should().HaveCount(2);
        decisions[0].Decision.Should().Be(Decision.Permit);
        decisions[1].Decision.Should().Be(Decision.Deny);
    }

    [Fact]
    async Task WhenDecideStreamingDuplicatesThenDeduplicates()
    {
        _handler.ResponseBody = "data: {\"decision\":\"PERMIT\"}\n\ndata: {\"decision\":\"PERMIT\"}\n\ndata: {\"decision\":\"DENY\"}\n\n";
        _handler.ResponseContentType = "text/event-stream";
        _handler.UseStream = true;
        var client = CreateClient();
        var sub = AuthorizationSubscription.Create("alice", "read", "doc");

        var decisions = new List<AuthorizationDecision>();
        await foreach (var d in client.Decide(sub))
        {
            decisions.Add(d);
            if (decisions.Count >= 2)
                break;
        }

        decisions.Should().HaveCount(2);
        decisions[0].Decision.Should().Be(Decision.Permit);
        decisions[1].Decision.Should().Be(Decision.Deny);
    }

    [Fact]
    async Task WhenMultiDecideOnceThenReturnsAllDecisions()
    {
        _handler.ResponseBody = """
            {
                "decisions":{
                    "sub-1":{"decision":"PERMIT"},
                    "sub-2":{"decision":"DENY"}
                }
            }
            """;
        var client = CreateClient();
        var multiSub = new MultiAuthorizationSubscription
        {
            Subscriptions = new Dictionary<string, AuthorizationSubscription>
            {
                ["sub-1"] = AuthorizationSubscription.Create("alice", "read", "doc1"),
                ["sub-2"] = AuthorizationSubscription.Create("bob", "write", "doc2"),
            },
        };

        var result = await client.MultiDecideOnceAsync(multiSub);

        result.Decisions.Should().HaveCount(2);
        result.Decisions["sub-1"].Decision.Should().Be(Decision.Permit);
        result.Decisions["sub-2"].Decision.Should().Be(Decision.Deny);
    }

    [Fact]
    async Task WhenMultiDecideOnceFailsThenReturnsIndeterminateForAll()
    {
        _handler.StatusCode = HttpStatusCode.InternalServerError;
        _handler.ResponseBody = "error";
        var client = CreateClient();
        var multiSub = new MultiAuthorizationSubscription
        {
            Subscriptions = new Dictionary<string, AuthorizationSubscription>
            {
                ["sub-1"] = AuthorizationSubscription.Create("alice", "read", "doc1"),
                ["sub-2"] = AuthorizationSubscription.Create("bob", "write", "doc2"),
            },
        };

        var result = await client.MultiDecideOnceAsync(multiSub);

        result.Decisions.Should().HaveCount(2);
        result.Decisions["sub-1"].Decision.Should().Be(Decision.Indeterminate);
        result.Decisions["sub-2"].Decision.Should().Be(Decision.Indeterminate);
    }

    [Fact]
    void WhenHttpsUrlThenNoInsecureWarning()
    {
        _ = CreateClient(baseUrl: "https://localhost:8443");
        _logger.DidNotReceive().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("insecure")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    async Task WhenDisposedThenThrowsObjectDisposed()
    {
        var client = CreateClient();
        client.Dispose();
        var sub = AuthorizationSubscription.Create("alice", "read", "doc");

        var act = () => client.DecideOnceAsync(sub);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        if (disposing)
        {
            _handler.Dispose();
        }
        _disposed = true;
    }

    private PdpClient CreateClient(
        string baseUrl = "https://localhost:8443",
        int timeoutMs = 5000)
    {
        var options = new PdpClientOptions
        {
            BaseUrl = baseUrl,
            AllowInsecureConnections = baseUrl.StartsWith("http://", StringComparison.Ordinal),
            TimeoutMs = timeoutMs,
            StreamingMaxRetries = 1,
            StreamingRetryBaseDelayMs = 10,
            StreamingRetryMaxDelayMs = 50,
        };
        return new PdpClient(_factory, options, _logger);
    }
}

internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    public string ResponseBody { get; set; } = "";
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
    public string ResponseContentType { get; set; } = "application/json";
    public bool UseStream { get; set; }
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        HttpContent content;
        if (UseStream)
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(ResponseBody));
            content = new StreamContent(stream);
        }
        else
        {
            content = new StringContent(ResponseBody, Encoding.UTF8, ResponseContentType);
        }

        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(ResponseContentType);

        return new HttpResponseMessage(StatusCode)
        {
            Content = content,
        };
    }
}
