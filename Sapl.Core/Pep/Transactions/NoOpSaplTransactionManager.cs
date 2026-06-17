namespace Sapl.Core.Pep.Transactions;

/// <summary>
/// The default transaction manager used when the host registers none. It opens no boundary and
/// runs the body directly, so enforcement behaves exactly as it did before transaction support
/// existed. Registering a real <see cref="ISaplTransactionManager"/> opts into rollback.
/// </summary>
public sealed class NoOpSaplTransactionManager : ISaplTransactionManager
{
    /// <summary>The shared, stateless instance.</summary>
    public static NoOpSaplTransactionManager Instance { get; } = new();

    public Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> body, CancellationToken cancellationToken) => body();
}
