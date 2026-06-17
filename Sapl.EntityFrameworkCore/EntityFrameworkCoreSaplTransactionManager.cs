using Microsoft.EntityFrameworkCore;
using Sapl.Core.Pep.Transactions;

namespace Sapl.EntityFrameworkCore;

/// <summary>
/// An <see cref="ISaplTransactionManager"/> backed by an EF Core <typeparamref name="TDbContext"/>.
/// It opens a database transaction with <c>BeginTransactionAsync</c>, runs the enforced body, and
/// commits only when the body completes without throwing. An enforcement failure (an
/// <see cref="Sapl.Core.Constraints.AccessDeniedException"/>) propagates out of the body, so the
/// transaction is disposed without a commit, which rolls back any writes the protected method
/// performed.
/// </summary>
/// <typeparam name="TDbContext">The EF Core context whose connection owns the transaction.</typeparam>
public sealed class EntityFrameworkCoreSaplTransactionManager<TDbContext> : ISaplTransactionManager
    where TDbContext : DbContext
{
    private readonly TDbContext _dbContext;

    public EntityFrameworkCoreSaplTransactionManager(TDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> body, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = await body().ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}
