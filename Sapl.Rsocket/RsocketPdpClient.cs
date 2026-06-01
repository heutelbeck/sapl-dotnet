using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RSocket;
using RSocket.Transports;
using Sapl.Core.Authorization;
using Sapl.Core.Client;

namespace Sapl.Rsocket;

/// <summary>
/// RSocket / protobuf transport against a SAPL Node RSocket port, behind the same
/// <see cref="IPolicyDecisionPoint"/> surface as the HTTP client. The route name
/// rides the per-request metadata as raw UTF-8 bytes, the subscription as protobuf
/// data; credentials ride the setup frame once per connection. The socket is opened
/// lazily and cached; a failed call drops it so the next call reconnects with fresh
/// credentials (the node keeps no hard connection timer, so an expired OAuth2 token
/// is only rejected on the next call). Every failure path fails closed to INDETERMINATE.
/// The underlying library exposes no per-stream cancel, so abandoning a stream early
/// only releases its server resources once the client is disposed.
/// </summary>
public sealed class RsocketPdpClient : IPolicyDecisionPoint, IAsyncDisposable
{
    private const string ProtobufMimeType = "application/protobuf";
    private const string AuthMimeType = "message/x.rsocket.authentication.v0";

    private const string RouteDecideOnce = "decide-once";
    private const string RouteDecide = "decide";
    private const string RouteMultiDecide = "multi-decide";
    private const string RouteMultiDecideAll = "multi-decide-all";
    private const string RouteMultiDecideAllOnce = "multi-decide-all-once";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private readonly RsocketPdpClientOptions _options;
    private readonly SaplProtoCodec _codec = new();
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly ILogger _logger;
    private volatile IRSocketTransport? _transport;
    private volatile RSocketClient? _client;

    public RsocketPdpClient(RsocketPdpClientOptions options, ILogger? logger = null)
    {
        options.Validate();
        _options = options;
        _logger = logger ?? NullLogger.Instance;
    }

    public Task<AuthorizationDecision> DecideOnceAsync(
        AuthorizationSubscription subscription,
        CancellationToken cancellationToken = default) =>
        RequestResponseAsync(
            RouteDecideOnce,
            _codec.EncodeSubscription(subscription),
            bytes => _codec.DecodeDecision(bytes),
            AuthorizationDecision.IndeterminateInstance,
            cancellationToken);

    public Task<MultiAuthorizationDecision> MultiDecideAllOnceAsync(
        MultiAuthorizationSubscription subscription,
        CancellationToken cancellationToken = default) =>
        RequestResponseAsync(
            RouteMultiDecideAllOnce,
            _codec.EncodeMultiSubscription(subscription),
            bytes => _codec.DecodeMultiDecision(bytes),
            MultiAuthorizationDecision.IndeterminateForAll(subscription),
            cancellationToken);

    public IAsyncEnumerable<AuthorizationDecision> Decide(
        AuthorizationSubscription subscription,
        CancellationToken cancellationToken = default) =>
        StreamAsync(
            RouteDecide,
            _codec.EncodeSubscription(subscription),
            bytes => _codec.DecodeDecision(bytes),
            () => [AuthorizationDecision.IndeterminateInstance],
            cancellationToken);

    public IAsyncEnumerable<IdentifiableAuthorizationDecision> MultiDecide(
        MultiAuthorizationSubscription subscription,
        CancellationToken cancellationToken = default) =>
        StreamAsync(
            RouteMultiDecide,
            _codec.EncodeMultiSubscription(subscription),
            bytes => _codec.DecodeIdentifiableDecision(bytes),
            () => subscription.Subscriptions.Keys.Select(id => new IdentifiableAuthorizationDecision
            {
                SubscriptionId = id,
                Decision = AuthorizationDecision.IndeterminateInstance,
            }),
            cancellationToken);

    public IAsyncEnumerable<MultiAuthorizationDecision> MultiDecideAll(
        MultiAuthorizationSubscription subscription,
        CancellationToken cancellationToken = default) =>
        StreamAsync(
            RouteMultiDecideAll,
            _codec.EncodeMultiSubscription(subscription),
            bytes => _codec.DecodeMultiDecision(bytes),
            () => [MultiAuthorizationDecision.IndeterminateForAll(subscription)],
            cancellationToken);

    private async Task<T> RequestResponseAsync<T>(
        string route,
        byte[] data,
        Func<byte[], T> decode,
        T failClosed,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(RequestTimeout);
        try
        {
            var client = await ConnectAsync().WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            var bytes = await client
                .RequestResponse(result => result.data.ToArray(), Sequence(data), Sequence(route))
                .WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            return decode(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RSocket {Route} failed, failing closed to INDETERMINATE.", route);
            InvalidateConnection();
            return failClosed;
        }
    }

    private async IAsyncEnumerable<T> StreamAsync<T>(
        string route,
        byte[] data,
        Func<byte[], T> decode,
        Func<IEnumerable<T>> failClosed,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        RSocketClient? client = null;
        try
        {
            client = await ConnectAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RSocket {Route} stream connect failed, failing closed.", route);
        }

        if (client is null)
        {
            InvalidateConnection();
            foreach (var value in failClosed())
            {
                yield return value;
            }

            yield break;
        }

        // RSocket.Core only offers the IObserver push API for streams, so bridge it
        // to a channel. The observer faults closed on a stream or decode error.
        var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions { SingleReader = true });
        var observer = new PayloadObserver<T>(channel.Writer, decode, failClosed, InvalidateConnection);
        _ = client.RequestStream(observer, Sequence(data), Sequence(route), int.MaxValue);

        await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    private sealed class PayloadObserver<T>(
        ChannelWriter<T> writer,
        Func<byte[], T> decode,
        Func<IEnumerable<T>> failClosed,
        Action onFault) : IObserver<(ReadOnlySequence<byte> Metadata, ReadOnlySequence<byte> Data)>
    {
        public void OnNext((ReadOnlySequence<byte> Metadata, ReadOnlySequence<byte> Data) value)
        {
            try
            {
                writer.TryWrite(decode(value.Data.ToArray()));
            }
            catch (Exception)
            {
                Fault();
            }
        }

        public void OnError(Exception error) => Fault();

        public void OnCompleted() => writer.TryComplete();

        private void Fault()
        {
            onFault();
            foreach (var fallback in failClosed())
            {
                writer.TryWrite(fallback);
            }

            writer.TryComplete();
        }
    }

    private async Task<RSocketClient> ConnectAsync()
    {
        if (_client is not null)
        {
            return _client;
        }

        await _connectLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_client is not null)
            {
                return _client;
            }

            var authMetadata = await BuildSetupAuthMetadataAsync().ConfigureAwait(false);
            var options = new RSocketOptions
            {
                DataMimeType = ProtobufMimeType,
                MetadataMimeType = authMetadata is null ? ProtobufMimeType : AuthMimeType,
                KeepAlive = TimeSpan.FromSeconds(30),
                Lifetime = TimeSpan.FromSeconds(60),
            };
            var transport = BuildTransport();
            _transport = transport;
            var client = new RSocketClient(transport, options);
            await client.ConnectAsync(options, [], authMetadata ?? []).ConfigureAwait(false);
            _client = client;
            return client;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private IRSocketTransport BuildTransport()
    {
        if (_options.Tls is null)
        {
            return new SocketTransport($"tcp://{_options.Host}:{_options.Port}");
        }

        return new SslStreamTransport(_options.Host, _options.Port, _options.Tls);
    }

    private async Task<byte[]?> BuildSetupAuthMetadataAsync()
    {
        if (_options.Username is not null)
        {
            return RsocketAuth.Simple(_options.Username, _options.Secret!);
        }

        if (_options.Token is not null)
        {
            return RsocketAuth.Bearer(_options.Token);
        }

        if (_options.TokenProvider is not null)
        {
            return RsocketAuth.Bearer(await _options.TokenProvider.GetAccessTokenAsync().ConfigureAwait(false));
        }

        return null;
    }

    private void InvalidateConnection() => _client = null;

    private static ReadOnlySequence<byte> Sequence(byte[] bytes) => new(bytes);

    private static ReadOnlySequence<byte> Sequence(string route) => new(Encoding.UTF8.GetBytes(route));

    public async ValueTask DisposeAsync()
    {
        if (_transport is not null)
        {
            try
            {
                await _transport.StopAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "RSocket transport teardown failed on dispose.");
            }
        }

        _connectLock.Dispose();
    }
}
