using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Sapl.AspNetCore.Extensions;
using Sapl.Core.Attributes;
using Sapl.Core.Authorization;
using Sapl.Core.Client;
using Sapl.Core.Pep.Constraints;
using Sapl.EntityFrameworkCore;
using Xunit;

namespace Sapl.EntityFrameworkCore.Tests;

/// <summary>
/// Proves the EF Core transaction boundary rolls back a protected method's writes when SAPL
/// enforcement denies after the write, across both enforcement paths (controller filter and
/// service DispatchProxy). SQLite with a kept-open connection is used because it honours real
/// transactions, unlike the EF Core in-memory provider.
/// </summary>
public sealed class EntityFrameworkCoreTransactionIntegrationTests
{
    private static JsonElement Constraint(object value) => JsonSerializer.SerializeToElement(value);

    private static AuthorizationDecision Permit(params object[] obligations) => new()
    {
        Decision = Decision.Permit,
        Obligations = obligations.Length == 0 ? null : obligations.Select(Constraint).ToArray(),
    };

    private static AuthorizationDecision Deny() => new() { Decision = Decision.Deny };

    private static readonly object OutputObligationThatFails = new { type = "fail-output" };
    private static readonly object DecisionObligationThatFails = new { type = "fail-decision" };

    [Theory]
    [InlineData(EnforcementPath.Controller)]
    [InlineData(EnforcementPath.Service)]
    async Task WhenPermitThenWriteCommitted(EnforcementPath path)
    {
        await using var fixture = await TransactionFixture.StartAsync(Permit());

        await fixture.WriteAsync(path);

        (await fixture.WidgetCountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData(EnforcementPath.Controller)]
    [InlineData(EnforcementPath.Service)]
    async Task WhenPreEnforceOutputObligationFailsThenWriteRolledBack(EnforcementPath path)
    {
        await using var fixture = await TransactionFixture.StartAsync(Permit(OutputObligationThatFails));

        await fixture.PreEnforceWriteExpectingDenialAsync(path);

        (await fixture.WidgetCountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(EnforcementPath.Controller)]
    [InlineData(EnforcementPath.Service)]
    async Task WhenPostEnforceDeniesThenWriteRolledBack(EnforcementPath path)
    {
        await using var fixture = await TransactionFixture.StartAsync(Deny());

        await fixture.PostEnforceWriteExpectingDenialAsync(path);

        (await fixture.WidgetCountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(EnforcementPath.Controller)]
    [InlineData(EnforcementPath.Service)]
    async Task WhenPostEnforceDecisionObligationFailsThenWriteRolledBack(EnforcementPath path)
    {
        await using var fixture = await TransactionFixture.StartAsync(Permit(DecisionObligationThatFails));

        await fixture.PostEnforceWriteExpectingDenialAsync(path);

        (await fixture.WidgetCountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(EnforcementPath.Controller)]
    [InlineData(EnforcementPath.Service)]
    async Task WhenPostEnforceOutputObligationFailsThenWriteRolledBack(EnforcementPath path)
    {
        await using var fixture = await TransactionFixture.StartAsync(Permit(OutputObligationThatFails));

        await fixture.PostEnforceWriteExpectingDenialAsync(path);

        (await fixture.WidgetCountAsync()).Should().Be(0);
    }

    private sealed class TransactionFixture : IAsyncDisposable
    {
        private readonly IHost _host;
        private readonly SqliteConnection _connection;

        private TransactionFixture(IHost host, SqliteConnection connection)
        {
            _host = host;
            _connection = connection;
        }

        public static async Task<TransactionFixture> StartAsync(AuthorizationDecision decision)
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var host = await BuildHost(connection, decision);

            using (var scope = host.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<WidgetDbContext>().Database.EnsureCreatedAsync();
            }

            return new TransactionFixture(host, connection);
        }

        public async Task WriteAsync(EnforcementPath path)
        {
            var response = await _host.GetTestClient().PostAsync(Route(path, "/permit"), content: null);
            ((int)response.StatusCode).Should().Be(200);
        }

        public async Task PreEnforceWriteExpectingDenialAsync(EnforcementPath path)
        {
            var response = await _host.GetTestClient().PostAsync(Route(path, "/pre"), content: null);
            ((int)response.StatusCode).Should().Be(403);
        }

        public async Task PostEnforceWriteExpectingDenialAsync(EnforcementPath path)
        {
            var response = await _host.GetTestClient().PostAsync(Route(path, "/post"), content: null);
            ((int)response.StatusCode).Should().Be(403);
        }

        public async Task<int> WidgetCountAsync()
        {
            using var scope = _host.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<WidgetDbContext>().Widgets.CountAsync();
        }

        public async ValueTask DisposeAsync()
        {
            _host.Dispose();
            await _connection.DisposeAsync();
        }

        private static string Route(EnforcementPath path, string action) =>
            path == EnforcementPath.Controller ? $"/controller{action}" : $"/service{action}";

        private static async Task<IHost> BuildHost(SqliteConnection connection, AuthorizationDecision decision)
        {
            var host = new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder.UseTestServer();
                    webBuilder.ConfigureServices(services =>
                    {
                        services.AddLogging();
                        services.AddControllers().AddApplicationPart(typeof(TransactionFixture).Assembly);

                        services.AddDbContext<WidgetDbContext>(options => options.UseSqlite(connection));

                        services.AddSapl(options => options.BaseUrl = "http://localhost");
                        services.RemoveAll<IPolicyDecisionPoint>();
                        services.AddSingleton<IPolicyDecisionPoint>(new StubPdp(decision));
                        services.AddSaplConstraintHandler<FailingObligationProvider>();
                        services.AddSaplService<IWidgetService, WidgetService>();
                        services.AddSaplEntityFrameworkCoreTransactions<WidgetDbContext>();
                    });
                    webBuilder.Configure(app =>
                    {
                        app.UseSaplAccessDenied();
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    });
                })
                .Build();

            await host.StartAsync();
            return host;
        }
    }

    private sealed class StubPdp(AuthorizationDecision decision) : IPolicyDecisionPoint
    {
        public Task<AuthorizationDecision> DecideOnceAsync(AuthorizationSubscription s, CancellationToken c = default) =>
            Task.FromResult(decision);

        public IAsyncEnumerable<AuthorizationDecision> Decide(AuthorizationSubscription s, CancellationToken c = default) =>
            throw new NotSupportedException();

        public Task<MultiAuthorizationDecision> MultiDecideAllOnceAsync(MultiAuthorizationSubscription s, CancellationToken c = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<IdentifiableAuthorizationDecision> MultiDecide(MultiAuthorizationSubscription s, CancellationToken c = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<MultiAuthorizationDecision> MultiDecideAll(MultiAuthorizationSubscription s, CancellationToken c = default) =>
            throw new NotSupportedException();
    }

    // Resolves two obligations to handlers that throw: one scoped to the output signal (an
    // output-stage failure) and one scoped to the decision signal (a decision-stage failure).
    private sealed class FailingObligationProvider : IConstraintHandlerProvider
    {
        public IReadOnlyList<ScopedHandler> GetConstraintHandlers(JsonElement constraint, IReadOnlySet<SignalType> supportedSignals)
        {
            if (IConstraintHandlerProvider.ConstraintIsOfType(constraint, "fail-output"))
            {
                var output = supportedSignals.First(signal => signal.Kind == SignalKind.Output);
                return [new ScopedHandler(new ConstraintHandler.Runner(Throw), output, 0)];
            }

            if (IConstraintHandlerProvider.ConstraintIsOfType(constraint, "fail-decision"))
            {
                return [new ScopedHandler(new ConstraintHandler.Runner(Throw), SignalType.Decision, 0)];
            }

            return [];
        }

        private static void Throw() => throw new InvalidOperationException("obligation discharge failed");
    }
}
