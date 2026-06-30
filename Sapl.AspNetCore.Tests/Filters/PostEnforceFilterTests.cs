using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Sapl.AspNetCore.Extensions;
using Sapl.Core.Attributes;
using Sapl.Core.Authorization;
using Sapl.Core.Client;

namespace Sapl.AspNetCore.Tests.Filters;

/// <summary>
/// PostEnforce must run the protected action and then ALWAYS consult the PDP and gate on the
/// decision, regardless of the shape of the action result. Mirrors the Spring PEP
/// (PostEnforcePolicyEnforcementPoint: proceed, then decideOnce, then deny on any non-permit),
/// so an action whose result is not a value-carrying body still cannot escape authorization.
/// </summary>
public class PostEnforceFilterTests
{
    public static IEnumerable<object[]> NonBodyResultEndpoints() =>
    [
        ["/post-enforce/not-found"],
        ["/post-enforce/no-content"],
        ["/post-enforce/redirect"],
        ["/post-enforce/null-body"],
    ];

    [Theory]
    [MemberData(nameof(NonBodyResultEndpoints))]
    async Task WhenActionReturnsNonBodyResultAndPolicyDeniesThenDecisionIsConsulted(string path)
    {
        var pdp = new RecordingPolicyDecisionPoint(AuthorizationDecision.DenyInstance);
        using var host = await CreateHost(pdp);

        await host.GetTestClient().GetAsync(path);

        pdp.DecideOnceCount.Should().BeGreaterThan(0,
            "PostEnforce must obtain a decision for every result shape, not only value-carrying bodies");
    }

    [Theory]
    [MemberData(nameof(NonBodyResultEndpoints))]
    async Task WhenActionReturnsNonBodyResultAndPolicyDeniesThenForbidden(string path)
    {
        var pdp = new RecordingPolicyDecisionPoint(AuthorizationDecision.DenyInstance);
        using var host = await CreateHost(pdp);

        var response = await host.GetTestClient().GetAsync(path);

        ((int)response.StatusCode).Should().Be(403,
            "a non-permit decision must deny the action regardless of its result shape");
    }

    [Fact]
    async Task WhenActionReturnsBodyAndPolicyDeniesThenForbidden()
    {
        var pdp = new RecordingPolicyDecisionPoint(AuthorizationDecision.DenyInstance);
        using var host = await CreateHost(pdp);

        var response = await host.GetTestClient().GetAsync("/post-enforce/body");

        pdp.DecideOnceCount.Should().BeGreaterThan(0);
        ((int)response.StatusCode).Should().Be(403);
    }

    private static async Task<IHost> CreateHost(IPolicyDecisionPoint pdp)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddLogging();
                    services.AddSapl(options => options.BaseUrl = "http://localhost:8443");
                    services.RemoveAll<IPolicyDecisionPoint>();
                    services.AddSingleton(pdp);
                    services.AddControllers().AddApplicationPart(typeof(PostEnforceFilterTests).Assembly);
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

    private sealed class RecordingPolicyDecisionPoint(AuthorizationDecision decision) : IPolicyDecisionPoint
    {
        private int _decideOnceCount;

        public int DecideOnceCount => _decideOnceCount;

        public Task<AuthorizationDecision> DecideOnceAsync(
            AuthorizationSubscription subscription, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _decideOnceCount);
            return Task.FromResult(decision);
        }

        public IAsyncEnumerable<AuthorizationDecision> Decide(
            AuthorizationSubscription subscription, CancellationToken cancellationToken = default) => EmptyStream();

        public Task<MultiAuthorizationDecision> MultiDecideAllOnceAsync(
            MultiAuthorizationSubscription subscription, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<IdentifiableAuthorizationDecision> MultiDecide(
            MultiAuthorizationSubscription subscription, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<MultiAuthorizationDecision> MultiDecideAll(
            MultiAuthorizationSubscription subscription, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private static async IAsyncEnumerable<AuthorizationDecision> EmptyStream()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}

[ApiController]
public sealed class PostEnforceResultShapesController : ControllerBase
{
    [PostEnforce]
    [HttpGet("/post-enforce/not-found")]
    public IActionResult ReturnsNotFound() => NotFound();

    [PostEnforce]
    [HttpGet("/post-enforce/no-content")]
    public IActionResult ReturnsNoContent() => NoContent();

    [PostEnforce]
    [HttpGet("/post-enforce/redirect")]
    public IActionResult ReturnsRedirect() => Redirect("/elsewhere");

    [PostEnforce]
    [HttpGet("/post-enforce/null-body")]
    public IActionResult ReturnsNullBody() => Ok((object?)null);

    [PostEnforce]
    [HttpGet("/post-enforce/body")]
    public IActionResult ReturnsBody() => Ok("classified");
}
