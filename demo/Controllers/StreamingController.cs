using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Sapl.AspNetCore.Streaming;
using Sapl.Core.Authorization;
using Sapl.Core.Enforcement;

namespace Sapl.Demo.Controllers;

[ApiController]
[Route("api/streaming")]
public sealed class StreamingController : ControllerBase
{
    private readonly EnforcementEngine _engine;

    public StreamingController(EnforcementEngine engine)
    {
        _engine = engine;
    }

    [HttpGet("heartbeat/till-denied")]
    public async Task HeartbeatTillDenied()
    {
        var sub = AuthorizationSubscription.Create("anonymous", "stream:heartbeat", "heartbeat");
        var ct = HttpContext.RequestAborted;

        var stream = _engine.EnforceTillDenied(
            sub,
            () => GenerateHeartbeats(ct),
            onDeny: _ =>
            {
                // Will be handled as AccessDeniedException in the stream
            },
            cancellationToken: ct);

        var wrappedStream = WrapTillDenied(stream, ct);
        await SseResultAdapter.WriteSseStreamAsync(HttpContext, wrappedStream, ct);
    }

    [HttpGet("heartbeat/drop-while-denied")]
    public async Task HeartbeatDropWhileDenied()
    {
        var sub = AuthorizationSubscription.Create("anonymous", "stream:heartbeat", "heartbeat");
        var ct = HttpContext.RequestAborted;

        var stream = _engine.EnforceDropWhileDenied(sub, () => GenerateHeartbeats(ct), ct);
        await SseResultAdapter.WriteSseStreamAsync(HttpContext, stream, ct);
    }

    [HttpGet("heartbeat/recoverable")]
    public async Task HeartbeatRecoverable()
    {
        var sub = AuthorizationSubscription.Create("anonymous", "stream:heartbeat", "heartbeat");
        var ct = HttpContext.RequestAborted;

        var outputStream = WrapRecoverable(sub, ct);
        await SseResultAdapter.WriteSseStreamAsync(HttpContext, outputStream, ct);
    }

    private async IAsyncEnumerable<object> WrapTillDenied(
        IAsyncEnumerable<object> source,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var channel = System.Threading.Channels.Channel.CreateUnbounded<object>();

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in source.WithCancellation(ct))
                {
                    await channel.Writer.WriteAsync(item, ct);
                }
            }
            catch (Core.Constraints.AccessDeniedException)
            {
                await channel.Writer.WriteAsync(
                    new { type = "ACCESS_DENIED", message = "Stream terminated by policy" }, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Normal
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }, ct);

        await foreach (var item in channel.Reader.ReadAllAsync(ct))
        {
            yield return item;
        }
    }

    private async IAsyncEnumerable<object> WrapRecoverable(
        AuthorizationSubscription sub,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var outputItems = System.Threading.Channels.Channel.CreateUnbounded<object>();

        var stream = _engine.EnforceRecoverableIfDenied(
            sub,
            () => GenerateHeartbeats(ct),
            onDeny: _ =>
            {
                outputItems.Writer.TryWrite(new { type = "ACCESS_SUSPENDED", message = "Waiting for re-authorization" });
            },
            onRecover: _ =>
            {
                outputItems.Writer.TryWrite(new { type = "ACCESS_RESTORED", message = "Authorization restored" });
            },
            cancellationToken: ct);

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in stream.WithCancellation(ct))
                {
                    await outputItems.Writer.WriteAsync(item, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal
            }
            finally
            {
                outputItems.Writer.TryComplete();
            }
        }, ct);

        await foreach (var item in outputItems.Reader.ReadAllAsync(ct))
        {
            yield return item;
        }
    }

    private static async IAsyncEnumerable<object> GenerateHeartbeats(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var seq = 0;
        while (!ct.IsCancellationRequested)
        {
            yield return new { seq, ts = DateTime.UtcNow.ToString("o") };
            seq++;
            await Task.Delay(2000, ct);
        }
    }
}
