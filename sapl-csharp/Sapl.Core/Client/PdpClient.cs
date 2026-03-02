using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Sapl.Core.Authorization;

namespace Sapl.Core.Client;

public sealed class PdpClient : IPolicyDecisionPoint, IDisposable
{
    internal const string ErrorAuthFailed = "PDP authentication failed. Check token or username/secret configuration.";
    internal const string ErrorConnectionTimeout = "PDP connection timed out after {TimeoutMs}ms.";
    internal const string ErrorHttpStatus = "PDP returned HTTP {StatusCode}: {Body}";
    internal const string ErrorStreamDisconnected = "PDP streaming connection lost, reconnecting in {DelayMs}ms (attempt {Attempt}).";

    private const string ApiDecideOnce = "/api/pdp/decide-once";
    private const string ApiDecide = "/api/pdp/decide";
    private const string ApiMultiDecideOnce = "/api/pdp/multi-decide-once";
    private const string ApiMultiDecide = "/api/pdp/multi-decide";
    private const string ApiMultiDecideAllOnce = "/api/pdp/multi-decide-all-once";
    private const string ApiMultiDecideAll = "/api/pdp/multi-decide-all";
    private const int MaxLogBodyLength = 500;
    private const int RetryEscalationThreshold = 5;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PdpClient> _logger;
    private readonly PdpClientOptions _options;
    private readonly string _decideOnceUrl;
    private readonly string _decideUrl;
    private readonly string _multiDecideOnceUrl;
    private readonly string _multiDecideUrl;
    private readonly string _multiDecideAllOnceUrl;
    private readonly string _multiDecideAllUrl;
    private readonly AuthenticationHeaderValue? _authHeader;
    private bool _disposed;

    public PdpClient(
        IHttpClientFactory httpClientFactory,
        PdpClientOptions options,
        ILogger<PdpClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options;

        options.Validate();

        var baseUrl = options.BaseUrl.TrimEnd('/');
        _decideOnceUrl = baseUrl + ApiDecideOnce;
        _decideUrl = baseUrl + ApiDecide;
        _multiDecideOnceUrl = baseUrl + ApiMultiDecideOnce;
        _multiDecideUrl = baseUrl + ApiMultiDecide;
        _multiDecideAllOnceUrl = baseUrl + ApiMultiDecideAllOnce;
        _multiDecideAllUrl = baseUrl + ApiMultiDecideAll;

        _authHeader = BuildAuthHeader(options);

        if (new Uri(baseUrl).Scheme == Uri.UriSchemeHttp)
        {
            _logger.LogWarning("PDP connection uses insecure HTTP. Use HTTPS in production.");
        }
    }

    public async Task<AuthorizationDecision> DecideOnceAsync(
        AuthorizationSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var body = subscription.ToJsonString();
        _logger.LogDebug("DecideOnce: {Subscription}", subscription.ToLoggableString());

        var result = await FetchOnceAsync(_decideOnceUrl, body, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return AuthorizationDecision.IndeterminateInstance;
        }

        var decision = ResponseValidator.ValidateDecisionResponse(result.Value, _logger);
        _logger.LogDebug("DecideOnce result: {Decision}", decision.Decision);
        return decision;
    }

    public async IAsyncEnumerable<AuthorizationDecision> Decide(
        AuthorizationSubscription subscription,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var body = subscription.ToJsonString();
        _logger.LogDebug("Decide (streaming): {Subscription}", subscription.ToLoggableString());

        AuthorizationDecision? previous = null;

        await foreach (var data in StreamWithRetryAsync(
            _decideUrl, body, cancellationToken).ConfigureAwait(false))
        {
            if (data is null)
            {
                var indeterminate = AuthorizationDecision.IndeterminateInstance;
                if (!indeterminate.Equals(previous))
                {
                    previous = indeterminate;
                    yield return indeterminate;
                }
                continue;
            }

            var decision = ResponseValidator.ParseDecisionFromJson(data, _logger);
            if (!decision.Equals(previous))
            {
                previous = decision;
                yield return decision;
            }
        }
    }

    public async Task<MultiAuthorizationDecision> MultiDecideOnceAsync(
        MultiAuthorizationSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var body = subscription.ToJsonString();
        _logger.LogDebug("MultiDecideOnce: {Subscription}", subscription.ToLoggableString());

        var result = await FetchOnceAsync(_multiDecideOnceUrl, body, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return MultiAuthorizationDecision.IndeterminateForAll(subscription);
        }

        var parsed = ResponseValidator.ParseMultiDecisionFromJson(
            result.Value.GetRawText(), _logger);
        return parsed ?? MultiAuthorizationDecision.IndeterminateForAll(subscription);
    }

    public async Task<MultiAuthorizationDecision> MultiDecideAllOnceAsync(
        MultiAuthorizationSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var body = subscription.ToJsonString();
        _logger.LogDebug("MultiDecideAllOnce: {Subscription}", subscription.ToLoggableString());

        var result = await FetchOnceAsync(_multiDecideAllOnceUrl, body, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return MultiAuthorizationDecision.IndeterminateForAll(subscription);
        }

        var parsed = ResponseValidator.ParseMultiDecisionFromJson(
            result.Value.GetRawText(), _logger);
        return parsed ?? MultiAuthorizationDecision.IndeterminateForAll(subscription);
    }

    public async IAsyncEnumerable<IdentifiableAuthorizationDecision> MultiDecide(
        MultiAuthorizationSubscription subscription,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var body = subscription.ToJsonString();
        _logger.LogDebug("MultiDecide (streaming): {Subscription}", subscription.ToLoggableString());

        await foreach (var data in StreamWithRetryAsync(
            _multiDecideUrl, body, cancellationToken).ConfigureAwait(false))
        {
            if (data is null)
            {
                foreach (var id in subscription.Subscriptions.Keys)
                {
                    yield return new IdentifiableAuthorizationDecision
                    {
                        SubscriptionId = id,
                        Decision = AuthorizationDecision.IndeterminateInstance,
                    };
                }
                continue;
            }

            var decision = ResponseValidator.ParseIdentifiableDecisionFromJson(data, _logger);
            if (decision is not null)
            {
                yield return decision;
            }
        }
    }

    public async IAsyncEnumerable<MultiAuthorizationDecision> MultiDecideAll(
        MultiAuthorizationSubscription subscription,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var body = subscription.ToJsonString();
        _logger.LogDebug("MultiDecideAll (streaming): {Subscription}", subscription.ToLoggableString());

        MultiAuthorizationDecision? previous = null;

        await foreach (var data in StreamWithRetryAsync(
            _multiDecideAllUrl, body, cancellationToken).ConfigureAwait(false))
        {
            if (data is null)
            {
                var indeterminate = MultiAuthorizationDecision.IndeterminateForAll(subscription);
                previous = indeterminate;
                yield return indeterminate;
                continue;
            }

            var decision = ResponseValidator.ParseMultiDecisionFromJson(data, _logger);
            if (decision is not null && !MultiDecisionEquals(decision, previous))
            {
                previous = decision;
                yield return decision;
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private async Task<JsonElement?> FetchOnceAsync(
        string url,
        string body,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_options.TimeoutMs);

        try
        {
            using var request = CreateRequest(HttpMethod.Post, url, body);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var client = _httpClientFactory.CreateClient("SaplPdp");
            using var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);

            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized
                or System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogError(ErrorAuthFailed);
            }

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await ReadTruncatedBodyAsync(response, cts.Token).ConfigureAwait(false);
                _logger.LogWarning(ErrorHttpStatus, (int)response.StatusCode, responseBody);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ErrorConnectionTimeout, _options.TimeoutMs);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "PDP request to {Url} failed.", url);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ResponseValidator.ErrorJsonParseFailed + "{Message}", ex.Message);
            return null;
        }
    }

    private async IAsyncEnumerable<string?> StreamWithRetryAsync(
        string url,
        string body,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var attempt = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            await foreach (var data in StreamSseAsync(url, body, cancellationToken).ConfigureAwait(false))
            {
                attempt = 0;
                yield return data;
            }

            if (cancellationToken.IsCancellationRequested)
                yield break;

            attempt++;
            if (_options.StreamingMaxRetries > 0 && attempt >= _options.StreamingMaxRetries)
            {
                _logger.LogError("PDP streaming max retries ({MaxRetries}) reached.", _options.StreamingMaxRetries);
                yield break;
            }

            var delayMs = CalculateBackoffDelay(attempt);

            if (attempt >= RetryEscalationThreshold)
            {
                _logger.LogError(ErrorStreamDisconnected, delayMs, attempt);
            }
            else
            {
                _logger.LogWarning(ErrorStreamDisconnected, delayMs, attempt);
            }

            yield return null;

            try
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
        }
    }

    private IAsyncEnumerable<string> StreamSseAsync(
        string url,
        string body,
        CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(64)
        {
            SingleWriter = true,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

        _ = FillSseChannelAsync(url, body, channel.Writer, cancellationToken);

        return channel.Reader.ReadAllAsync(cancellationToken);
    }

    private async Task FillSseChannelAsync(
        string url,
        string body,
        ChannelWriter<string> writer,
        CancellationToken cancellationToken)
    {
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCts.CancelAfter(_options.TimeoutMs);

        HttpResponseMessage? response = null;
        Stream? stream = null;

        try
        {
            using var request = CreateRequest(HttpMethod.Post, url, body);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            var client = _httpClientFactory.CreateClient("SaplPdp");
            response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                connectCts.Token).ConfigureAwait(false);

            connectCts.CancelAfter(Timeout.Infinite);

            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized
                or System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogError(ErrorAuthFailed);
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await ReadTruncatedBodyAsync(response, cancellationToken).ConfigureAwait(false);
                _logger.LogWarning(ErrorHttpStatus, (int)response.StatusCode, responseBody);
                return;
            }

            stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var parser = new SseParser();
            var buffer = new byte[8192];

            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                    return;

                var chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                foreach (var data in parser.ProcessChunk(chunk))
                {
                    if (!string.IsNullOrWhiteSpace(data))
                    {
                        await writer.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ErrorConnectionTimeout, _options.TimeoutMs);
        }
        catch (SseBufferOverflowException ex)
        {
            _logger.LogWarning(ex, "SSE buffer overflow, reconnecting.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "PDP streaming connection to {Url} failed.", url);
        }
        finally
        {
            stream?.Dispose();
            response?.Dispose();
            writer.Complete();
        }
    }

    private int CalculateBackoffDelay(int attempt)
    {
        var baseDelay = Math.Min(
            _options.StreamingRetryBaseDelayMs * Math.Pow(2, attempt - 1),
            _options.StreamingRetryMaxDelayMs);
        var jitter = Random.Shared.NextDouble() * 0.5;
        return (int)Math.Round(baseDelay * (0.5 + jitter));
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url, string body)
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (_authHeader is not null)
        {
            request.Headers.Authorization = _authHeader;
        }
        return request;
    }

    private static AuthenticationHeaderValue? BuildAuthHeader(PdpClientOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Token))
        {
            return new AuthenticationHeaderValue("Bearer", options.Token);
        }

        if (!string.IsNullOrWhiteSpace(options.Username) &&
            !string.IsNullOrWhiteSpace(options.Secret))
        {
            var encoded = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{options.Username}:{options.Secret}"));
            return new AuthenticationHeaderValue("Basic", encoded);
        }

        return null;
    }

    private static async Task<string> ReadTruncatedBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return body.Length > MaxLogBodyLength
                ? body[..MaxLogBodyLength] + "..."
                : body;
        }
        catch
        {
            return "(unable to read response body)";
        }
    }

    private static bool MultiDecisionEquals(
        MultiAuthorizationDecision a,
        MultiAuthorizationDecision? b)
    {
        if (b is null)
            return false;
        if (a.Decisions.Count != b.Decisions.Count)
            return false;
        foreach (var (key, decisionA) in a.Decisions)
        {
            if (!b.Decisions.TryGetValue(key, out var decisionB))
                return false;
            if (!decisionA.Equals(decisionB))
                return false;
        }
        return true;
    }
}
