using System.Buffers;
using System.Text;
using System.Threading.Channels;
using FluentAssertions;
using Sapl.Core.Authorization;

namespace Sapl.Rsocket.Tests;

/// <summary>
/// The RSocket streaming contract is that a subscription never terminates on a
/// transport error or a server-side stream completion. Both end the current
/// connection gracefully, the loop emits INDETERMINATE, and it reconnects with
/// bounded backoff forever. Only consumer cancellation ends the stream.
///
/// The RSocket.Core library exposes streams only through a concrete
/// <c>RSocketClient</c> with no interface seam, so the full transport cannot be
/// faked. The pivot the never-terminate property turns on is
/// <see cref="RsocketPdpClient.PayloadObserver{T}"/>: it bridges the library push
/// API to a channel and must complete that channel (not fault it) on OnError and
/// OnCompleted. A faulted reader would propagate out of the drain loop and
/// terminate the subscription; a completed reader lets the loop fall through to
/// reconnect. These tests pin that behaviour and the surrounding drain-then-
/// reconnect loop using a deterministic in-memory stand-in for the per-connection
/// channel the production loop drives.
/// </summary>
public sealed class RsocketReconnectTests
{
    private static (ReadOnlySequence<byte> Metadata, ReadOnlySequence<byte> Data) Payload(string decisionJson)
    {
        var bytes = Encoding.UTF8.GetBytes(decisionJson);
        return (ReadOnlySequence<byte>.Empty, new ReadOnlySequence<byte>(bytes));
    }

    private static RsocketPdpClient.PayloadObserver<string> StringObserver(ChannelWriter<string> writer) =>
        new(writer, bytes => Encoding.UTF8.GetString(bytes));

    [Fact]
    void WhenServerCompletesStreamThenObserverCompletesChannelGracefully()
    {
        var channel = Channel.CreateUnbounded<string>();
        var observer = StringObserver(channel.Writer);

        observer.OnNext(Payload("PERMIT"));
        observer.OnCompleted();

        channel.Reader.TryRead(out var first).Should().BeTrue();
        first.Should().Be("PERMIT");
        channel.Reader.Completion.IsFaulted.Should().BeFalse();
        channel.Reader.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    void WhenTransportErrorsThenObserverCompletesChannelWithoutFaulting()
    {
        var channel = Channel.CreateUnbounded<string>();
        var observer = StringObserver(channel.Writer);

        observer.OnNext(Payload("DENY"));
        observer.OnError(new IOException("transport dropped"));

        channel.Reader.TryRead(out var first).Should().BeTrue();
        first.Should().Be("DENY");
        channel.Reader.Completion.IsFaulted.Should().BeFalse();
        channel.Reader.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    async Task WhenConnectionEndsRepeatedlyThenStreamReconnectsAndEmitsIndeterminateInsteadOfTerminating()
    {
        // Drives the production drain-then-reconnect shape: each generation feeds a
        // fresh channel through PayloadObserver, ends (server complete or transport
        // error), and the loop emits a failClosed INDETERMINATE before the next
        // generation. The stream ends ONLY when the consumer cancellation fires.
        var generations = new Queue<Action<RsocketPdpClient.PayloadObserver<string>>>(
        [
            obs =>
            {
                obs.OnNext(Payload("PERMIT"));
                obs.OnCompleted();
            },
            obs =>
            {
                obs.OnNext(Payload("DENY"));
                obs.OnError(new IOException("transport dropped"));
            },
        ]);

        const string indeterminate = "INDETERMINATE";
        using var cts = new CancellationTokenSource();
        var emitted = new List<string>();

        while (!cts.Token.IsCancellationRequested)
        {
            var channel = Channel.CreateUnbounded<string>();
            var observer = StringObserver(channel.Writer);

            if (generations.Count > 0)
            {
                generations.Dequeue()(observer);
            }
            else
            {
                observer.OnCompleted();
            }

            await foreach (var item in channel.Reader.ReadAllAsync(cts.Token))
            {
                emitted.Add(item);
            }

            // Graceful drain completion means the loop survives to fail closed and
            // reconnect rather than terminating with an exception.
            emitted.Add(indeterminate);

            if (generations.Count == 0)
            {
                cts.Cancel();
            }
        }

        emitted.Should().ContainInOrder("PERMIT", indeterminate, "DENY", indeterminate);
        emitted.Should().NotBeEmpty();
    }

    [Fact]
    void WhenDecodeThrowsThenObserverCompletesChannelDefensively()
    {
        var channel = Channel.CreateUnbounded<AuthorizationDecision>();
        var observer = new RsocketPdpClient.PayloadObserver<AuthorizationDecision>(
            channel.Writer,
            _ => throw new FormatException("corrupt frame"));

        observer.OnNext(Payload("garbage"));

        channel.Reader.Completion.IsFaulted.Should().BeFalse();
        channel.Reader.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }
}
