namespace Sapl.Core.Pep.Transactions;

/// <summary>
/// Opt-in transaction boundary around an enforced invocation. The enforcement paths run the
/// protected method and the enforcement that depends on its result inside a single boundary,
/// so a denial that surfaces after a write rolls those writes back. Rollback happens by letting
/// the enforcement failure (an <see cref="Sapl.Core.Constraints.AccessDeniedException"/>)
/// propagate out of <see cref="ExecuteInTransactionAsync{T}"/> without committing.
/// </summary>
/// <remarks>
/// The core stays persistence-agnostic. A concrete manager (for example one backed by EF Core)
/// is registered by the host; when none is registered the enforcement paths use
/// <see cref="NoOpSaplTransactionManager"/>, which runs the body directly and changes nothing.
/// </remarks>
public interface ISaplTransactionManager
{
    /// <summary>
    /// Runs <paramref name="body"/> inside a transaction. The transaction commits only when the
    /// body completes without throwing. An exception (including an
    /// <see cref="Sapl.Core.Constraints.AccessDeniedException"/> raised by post-method enforcement)
    /// leaves the transaction uncommitted, rolling back any writes the body performed, and then
    /// propagates to the caller.
    /// </summary>
    /// <typeparam name="T">The body's result type.</typeparam>
    /// <param name="body">The protected invocation plus its dependent enforcement.</param>
    /// <param name="cancellationToken">Cancels the boundary and the underlying transaction.</param>
    /// <returns>The value produced by <paramref name="body"/> on success.</returns>
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> body, CancellationToken cancellationToken);
}
