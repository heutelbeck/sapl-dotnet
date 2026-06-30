using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sapl.Core.Authorization;
using Sapl.Core.Client;

namespace Sapl.Core.Tests.Client;

// Operational resilience of the HTTP streaming decision paths: a dead-but-open
// socket must fail closed, and a sustained outage must not flood the PEP with
// repeated INDETERMINATEs. Mirrors the current Spring PEP (.timeout(firstItem,
// perItem) and distinctUntilChanged placed outside retryWhen).
public class PdpClientStreamingResilienceTests
{
    // CR-11: a silently dead-but-open SSE connection must time out on inactivity,
    // fail closed to INDETERMINATE and re-enter the reconnect loop.
    public class HalfOpenConnectionScenario
    {
        [Fact(DisplayName = "Single stream: a silent SSE socket times out, emits INDETERMINATE and reconnects")]
        async Task WhenSseSocketGoesSilentThenFailsClosedToIndeterminate()
        {
            using var handler = new HalfOpenSseHandler("data: {\"decision\":\"PERMIT\"}\n\n");
            var client = BuildClient(handler, timeoutMs: 250, streamInactivityTimeoutMs: 250);
            var subscription = AuthorizationSubscription.Create("alice", "read", "doc");

            using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var decisions = new List<AuthorizationDecision>();
            try
            {
                await foreach (var decision in client.Decide(subscription, safety.Token))
                {
                    decisions.Add(decision);
                    if (decisions.Count >= 2)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Safety bound tripped: the stream stayed pinned on its last decision.
            }

            decisions.Should().HaveCountGreaterThanOrEqualTo(2);
            decisions[0].Decision.Should().Be(Decision.Permit);
            decisions[1].Decision.Should().Be(Decision.Indeterminate);
        }
    }

    // CR-07: during one sustained outage the multi-decision paths must collapse
    // to exactly one INDETERMINATE (per subscription), with dedup state surviving
    // every reconnect attempt rather than re-emitting on each backoff cycle.
    public class SustainedOutageScenario
    {
        [Fact(DisplayName = "MultiDecideAll: a multi-cycle outage collapses to a single INDETERMINATE")]
        async Task WhenPdpUnreachableThenMultiDecideAllEmitsSingleIndeterminate()
        {
            using var handler = FailingHandler();
            var client = BuildClient(handler, retryBaseDelayMs: 1, retryMaxDelayMs: 2);
            var subscription = TwoSubscriptions();

            var emissions = await CollectDuringOutageAsync(
                client.MultiDecideAll, subscription, TimeSpan.FromMilliseconds(400));

            emissions.Should().ContainSingle();
            emissions[0].Decisions.Values.Should()
                .OnlyContain(decision => decision.Decision == Decision.Indeterminate);
        }

        [Fact(DisplayName = "MultiDecide: a multi-cycle outage yields one INDETERMINATE per subscription")]
        async Task WhenPdpUnreachableThenMultiDecideEmitsOneIndeterminatePerSubscription()
        {
            using var handler = FailingHandler();
            var client = BuildClient(handler, retryBaseDelayMs: 1, retryMaxDelayMs: 2);
            var subscription = TwoSubscriptions();

            var emissions = await CollectDuringOutageAsync(
                client.MultiDecide, subscription, TimeSpan.FromMilliseconds(400));

            emissions.Should().HaveCount(2);
            emissions.Select(e => e.SubscriptionId).Should()
                .BeEquivalentTo(new[] { "sub-1", "sub-2" });
            emissions.Should()
                .OnlyContain(e => e.Decision.Decision == Decision.Indeterminate);
        }

        private static MockHttpMessageHandler FailingHandler() =>
            new() { StatusCode = HttpStatusCode.InternalServerError, ResponseBody = "outage" };

        private static async Task<List<T>> CollectDuringOutageAsync<T>(
            Func<MultiAuthorizationSubscription, CancellationToken, IAsyncEnumerable<T>> stream,
            MultiAuthorizationSubscription subscription,
            TimeSpan window)
        {
            using var bound = new CancellationTokenSource(window);
            var emissions = new List<T>();
            try
            {
                await foreach (var emission in stream(subscription, bound.Token))
                {
                    emissions.Add(emission);
                }
            }
            catch (OperationCanceledException)
            {
                // Window closed; the outage is still ongoing.
            }
            return emissions;
        }
    }

    private static MultiAuthorizationSubscription TwoSubscriptions() => new()
    {
        Subscriptions = new Dictionary<string, AuthorizationSubscription>
        {
            ["sub-1"] = AuthorizationSubscription.Create("alice", "read", "doc1"),
            ["sub-2"] = AuthorizationSubscription.Create("bob", "write", "doc2"),
        },
    };

    private static PdpClient BuildClient(
        HttpMessageHandler handler,
        int timeoutMs = 5000,
        int retryBaseDelayMs = 1000,
        int retryMaxDelayMs = 30000,
        int streamInactivityTimeoutMs = 60000)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("SaplPdp").Returns(_ => new HttpClient(handler));

        var options = new PdpClientOptions
        {
            BaseUrl = "https://localhost:8443",
            TimeoutMs = timeoutMs,
            StreamInactivityTimeoutMs = streamInactivityTimeoutMs,
            StreamingRetryBaseDelayMs = retryBaseDelayMs,
            StreamingRetryMaxDelayMs = retryMaxDelayMs,
        };
        return new PdpClient(factory, options, Substitute.For<ILogger<PdpClient>>());
    }

    // Returns 200 + text/event-stream, emits one frame, then leaves the socket
    // open and silent (no further bytes, no FIN/RST): a half-open connection.
    private sealed class HalfOpenSseHandler : HttpMessageHandler
    {
        private readonly string _firstFrame;

        public HalfOpenSseHandler(string firstFrame) => _firstFrame = firstFrame;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var content = new StreamContent(new HalfOpenSseStream(_firstFrame));
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class HalfOpenSseStream : Stream
    {
        private readonly byte[] _firstFrame;
        private bool _firstRead = true;

        public HalfOpenSseStream(string firstFrame) =>
            _firstFrame = Encoding.UTF8.GetBytes(firstFrame);

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_firstRead)
            {
                _firstRead = false;
                _firstFrame.AsSpan().CopyTo(buffer.Span);
                return _firstFrame.Length;
            }

            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return 0;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }

    // CR-11 follow-up: the SSE inactivity deadline must be the (large) stream
    // inactivity window, not the (short) connect timeout. A decision stream that is
    // merely quiet between decisions must keep delivering, not fail closed mid-stream.
    public class QuietWithinInactivityWindowScenario
    {
        [Fact(DisplayName = "A stream quiet past the connect timeout still delivers a later decision within the inactivity window")]
        async Task WhenQuietWithinInactivityWindowThenLaterDecisionDelivered()
        {
            using var handler = new DelayedSecondFrameSseHandler(
                "data: {\"decision\":\"PERMIT\"}\n\n",
                "data: {\"decision\":\"DENY\"}\n\n",
                delayMs: 500);
            var client = BuildClient(handler, timeoutMs: 150, streamInactivityTimeoutMs: 3000);
            var subscription = AuthorizationSubscription.Create("alice", "read", "doc");

            using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var decisions = new List<AuthorizationDecision>();
            await foreach (var decision in client.Decide(subscription, safety.Token))
            {
                decisions.Add(decision);
                if (decisions.Count >= 2)
                {
                    break;
                }
            }

            decisions[0].Decision.Should().Be(Decision.Permit);
            decisions[1].Decision.Should().Be(Decision.Deny,
                "a stream merely quiet within the inactivity window must keep delivering decisions, not fail closed at the connect timeout");
        }
    }

    // 200 + text/event-stream: emits one frame, waits delayMs, emits a second frame,
    // then leaves the socket open silently.
    private sealed class DelayedSecondFrameSseHandler : HttpMessageHandler
    {
        private readonly string _first;
        private readonly string _second;
        private readonly int _delayMs;

        public DelayedSecondFrameSseHandler(string first, string second, int delayMs)
        {
            _first = first;
            _second = second;
            _delayMs = delayMs;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var content = new StreamContent(new DelayedSecondFrameSseStream(_first, _second, _delayMs));
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class DelayedSecondFrameSseStream : Stream
    {
        private readonly byte[] _first;
        private readonly byte[] _second;
        private readonly int _delayMs;
        private int _read;

        public DelayedSecondFrameSseStream(string first, string second, int delayMs)
        {
            _first = Encoding.UTF8.GetBytes(first);
            _second = Encoding.UTF8.GetBytes(second);
            _delayMs = delayMs;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            switch (_read++)
            {
                case 0:
                    _first.AsSpan().CopyTo(buffer.Span);
                    return _first.Length;
                case 1:
                    await Task.Delay(_delayMs, cancellationToken).ConfigureAwait(false);
                    _second.AsSpan().CopyTo(buffer.Span);
                    return _second.Length;
                default:
                    await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                    return 0;
            }
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
