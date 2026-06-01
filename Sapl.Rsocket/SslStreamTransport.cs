using System.IO.Pipelines;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using RSocket;

namespace Sapl.Rsocket;

/// <summary>
/// An <see cref="IRSocketTransport"/> over a TLS-wrapped TCP socket. RSocket.Core's
/// own transport is plaintext only, so this establishes the SslStream itself and
/// exposes it as the duplex pipe RSocket.Core frames over. The configured CA is
/// trusted as a custom root while server hostname is still enforced.
/// </summary>
internal sealed class SslStreamTransport(string host, int port, RsocketTlsOptions tls) : IRSocketTransport
{
    private TcpClient? _tcpClient;
    private SslStream? _sslStream;

    public PipeReader Input { get; private set; } = null!;

    public PipeWriter Output { get; private set; } = null!;

    public async Task StartAsync(CancellationToken cancel)
    {
        var ca = X509CertificateLoader.LoadCertificateFromFile(tls.CaPemPath);
        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(host, port, cancel).ConfigureAwait(false);
        _sslStream = new SslStream(
            _tcpClient.GetStream(),
            leaveInnerStreamOpen: false,
            userCertificateValidationCallback: (_, certificate, _, errors) => TrustsCa(certificate, errors, ca));
        await _sslStream.AuthenticateAsClientAsync(tls.ServerName ?? host).ConfigureAwait(false);
        Input = PipeReader.Create(_sslStream);
        Output = PipeWriter.Create(_sslStream);
    }

    public async Task StopAsync()
    {
        if (_sslStream is not null)
        {
            await _sslStream.DisposeAsync().ConfigureAwait(false);
        }

        _tcpClient?.Dispose();
    }

    private static bool TrustsCa(X509Certificate? certificate, SslPolicyErrors errors, X509Certificate2 ca)
    {
        if (errors == SslPolicyErrors.None)
        {
            return true;
        }

        if (errors != SslPolicyErrors.RemoteCertificateChainErrors || certificate is null)
        {
            return false;
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(ca);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(new X509Certificate2(certificate));
    }
}
