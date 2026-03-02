using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sapl.AspNetCore.Middleware;
using Sapl.Core.Constraints;

namespace Sapl.AspNetCore.Tests.Middleware;

public class AccessDeniedMiddlewareTests
{
    [Fact]
    async Task WhenAccessDeniedExceptionThenReturns403()
    {
        using var host = await CreateHost(
            _ => throw new AccessDeniedException("test denied"));

        var client = host.GetTestClient();
        var response = await client.GetAsync("/test");

        ((int)response.StatusCode).Should().Be(403);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Access denied");
    }

    [Fact]
    async Task WhenNoExceptionThenPassesThrough()
    {
        using var host = await CreateHost(
            async ctx =>
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.WriteAsync("OK");
            });

        var client = host.GetTestClient();
        var response = await client.GetAsync("/test");

        ((int)response.StatusCode).Should().Be(200);
    }

    [Fact]
    async Task WhenOtherExceptionThenNotCaught()
    {
        using var host = await CreateHost(
            _ => throw new InvalidOperationException("other error"));

        var client = host.GetTestClient();
        var act = () => client.GetAsync("/test");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static async Task<IHost> CreateHost(RequestDelegate handler)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddLogging();
                });
                webBuilder.Configure(app =>
                {
                    app.UseMiddleware<AccessDeniedMiddleware>();
                    app.Run(handler);
                });
            })
            .Build();

        await host.StartAsync();
        return host;
    }
}
