namespace Sapl.Core.Client.Auth;

/// <summary>Supplies bearer access tokens for the PDP client, refreshing as needed.</summary>
public interface IAccessTokenProvider
{
    ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>Drops any cached token so the next call acquires a fresh one.</summary>
    void Invalidate();
}
