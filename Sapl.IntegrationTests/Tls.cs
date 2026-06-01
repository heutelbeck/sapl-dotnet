using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Sapl.IntegrationTests;

/// <summary>Test TLS helpers for connecting to a node that serves the fixture cert pair.</summary>
internal static class Tls
{
    /// <summary>
    /// An HttpClient handler that trusts only the given CA as a custom root. Server
    /// hostname is still enforced; only the unknown-root chain error is bridged.
    /// </summary>
    public static SocketsHttpHandler TrustingHandler(string caPemPath)
    {
        var ca = X509CertificateLoader.LoadCertificateFromFile(caPemPath);
        return new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
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
                },
            },
        };
    }
}
