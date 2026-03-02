using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Sapl.Core.Authorization;

namespace Sapl.AspNetCore.Streaming;

public static class SseResultAdapter
{
    public static async Task WriteSseStreamAsync<T>(
        HttpContext httpContext,
        IAsyncEnumerable<T> source,
        CancellationToken cancellationToken = default)
    {
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";
        httpContext.Response.StatusCode = StatusCodes.Status200OK;

        await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var json = JsonSerializer.Serialize(item, SerializerDefaults.Options);
            await httpContext.Response.WriteAsync($"data: {json}\n\n", cancellationToken).ConfigureAwait(false);
            await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
