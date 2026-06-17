using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sapl.Core.Pep.Transactions;

namespace Sapl.EntityFrameworkCore;

/// <summary>
/// Opt-in registration for EF Core-backed SAPL transaction boundaries. Registering the manager
/// turns on rollback: when enforcement denies after a protected method has written, the write is
/// rolled back. Without this registration the enforcement paths run without a boundary and behave
/// exactly as before.
/// </summary>
public static class SaplEntityFrameworkCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="ISaplTransactionManager"/> that wraps enforced invocations in a
    /// transaction on <typeparamref name="TDbContext"/>. The context must already be registered
    /// (for example via <c>AddDbContext{TDbContext}</c>). The manager is scoped, matching the
    /// per-request lifetime of the context and the SAPL filters and interceptor.
    /// </summary>
    /// <typeparam name="TDbContext">The EF Core context whose connection owns the transaction.</typeparam>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddSaplEntityFrameworkCoreTransactions<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddScoped<ISaplTransactionManager, EntityFrameworkCoreSaplTransactionManager<TDbContext>>();
        return services;
    }
}
