using System.Text;

namespace Sapl.Rsocket;

/// <summary>
/// Encodes RSocket setup-frame authentication metadata in the well-known format
/// the SAPL Node reads via io.rsocket.metadata.AuthMetadataCodec. Simple carries
/// a username and password, Bearer a token (a SAPL API key or a JWT).
/// </summary>
internal static class RsocketAuth
{
    private const byte WellKnownSimple = 0x80;
    private const byte WellKnownBearer = 0x81;

    public static byte[] Simple(string username, string password)
    {
        var user = Encoding.UTF8.GetBytes(username);
        var pass = Encoding.UTF8.GetBytes(password);
        var buffer = new byte[1 + 2 + user.Length + pass.Length];
        buffer[0] = WellKnownSimple;
        buffer[1] = (byte)(user.Length >> 8);
        buffer[2] = (byte)(user.Length & 0xFF);
        user.CopyTo(buffer, 3);
        pass.CopyTo(buffer, 3 + user.Length);
        return buffer;
    }

    public static byte[] Bearer(string token)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(token);
        var buffer = new byte[1 + tokenBytes.Length];
        buffer[0] = WellKnownBearer;
        tokenBytes.CopyTo(buffer, 1);
        return buffer;
    }
}
