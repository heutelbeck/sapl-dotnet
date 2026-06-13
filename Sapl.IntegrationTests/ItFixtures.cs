using Sapl.Core.Authorization;

namespace Sapl.IntegrationTests;

internal static class ItFixtures
{
    public static readonly AuthorizationSubscription PermitSubscription =
        AuthorizationSubscription.Create("alice", "read", "doc-1");

    // Argon2id hash of the plaintext below, reused verbatim from the engine-side
    // IT fixtures. The node stores the hash, the client sends the plaintext.
    public const string CredentialHash =
        "$argon2id$v=19$m=16384,t=2,p=1$FttHTp38SkUUzUA4cA5Epg$QjzIAdvmNGP0auVlkCDpjrgr2LHeM5ul0BYLr7QKwBM";

    public const string PlaintextCredential = "sapl_7A7ByyQd6U_5nTv3KXXLPiZ8JzHQywF9gww2v0iuA3j";

    // The public middle segment of the wire token sapl_<id>_<secret>. The node
    // indexes api-key users by this id and refuses to start if it is missing.
    public const string ApiKeyId = "7A7ByyQd6U";
}
