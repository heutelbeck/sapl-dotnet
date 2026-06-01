using Microsoft.Extensions.Logging.Abstractions;
using Sapl.Core.Client;

namespace Sapl.IntegrationTests;

/// <summary>Builds a <see cref="PdpClient"/> over a plain shared HttpClient for ITs.</summary>
internal static class PdpClients
{
    public static PdpClient Create(PdpClientOptions options, HttpMessageHandler? handler = null) =>
        new(new SingleHttpClientFactory(handler), options, NullLogger<PdpClient>.Instance);

    private sealed class SingleHttpClientFactory(HttpMessageHandler? handler) : IHttpClientFactory
    {
        private readonly HttpClient _client = handler is null ? new HttpClient() : new HttpClient(handler);

        public HttpClient CreateClient(string name) => _client;
    }
}
