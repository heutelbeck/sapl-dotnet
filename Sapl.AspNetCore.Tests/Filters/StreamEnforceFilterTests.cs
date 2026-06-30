using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sapl.AspNetCore.Enforcement;
using Sapl.AspNetCore.Filters;
using Sapl.AspNetCore.Interception;
using Sapl.Core.Attributes;
using Sapl.Core.Client;
using Sapl.Core.Pep.Constraints;
using Sapl.Core.Pep.Enforcement;

namespace Sapl.AspNetCore.Tests.Filters;

/// <summary>
/// Operational contract: a controller action carrying <see cref="StreamEnforceAttribute"/> is a
/// streaming endpoint. Per the Spring source of truth (StreamEnforcePolicyEnforcementPoint, which
/// throws IllegalStateException ERROR_UNSUPPORTED_RETURN_TYPE when the return type is not a Flux),
/// a misannotated action that produces anything other than a stream must FAIL CLOSED: the request
/// is rejected, never served unenforced. The domain-proxy path already does this (SaplProxy throws
/// InvalidOperationException); the MVC filter must behave identically. Traceability: STREAM-FSM-01.
/// </summary>
public class StreamEnforceFilterTests
{
    [StreamEnforce]
    private static Task<List<string>> NonStreamingAction() =>
        Task.FromResult(new List<string> { "top-secret" });

    public static TheoryData<string, IActionResult> NonStreamingResults() => new()
    {
        { "materialized list of secrets", new ObjectResult(new List<string> { "top-secret" }) },
        { "content result", new ContentResult { Content = "top-secret" } },
        { "null object value", new ObjectResult(null) },
        { "empty result", new EmptyResult() },
    };

    [Theory]
    [MemberData(nameof(NonStreamingResults))]
    async Task WhenStreamEnforcedActionReturnsNonStreamThenRequestIsRejected(string scenario, IActionResult result)
    {
        _ = scenario;
        var filter = CreateFilter();
        var executing = CreateExecutingContext();
        var executed = CreateExecutedContext(executing, result);

        var act = async () => await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [MemberData(nameof(NonStreamingResults))]
    async Task WhenStreamEnforcedActionReturnsNonStreamThenUnenforcedResultIsNotServed(string scenario, IActionResult result)
    {
        _ = scenario;
        var filter = CreateFilter();
        var executing = CreateExecutingContext();
        var executed = CreateExecutedContext(executing, result);

        try
        {
            await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        }
        catch (InvalidOperationException)
        {
            // Rejection is the required outcome; the assertion below proves the unenforced
            // payload never survives as the response.
        }

        executed.Result.Should().NotBeSameAs(result);
    }

    private static StreamEnforceFilter CreateFilter()
    {
        var pdp = Substitute.For<IPolicyDecisionPoint>();
        var engine = new EnforcementEngine(pdp, Array.Empty<IConstraintHandlerProvider>());
        var contextFactory = new HttpSubscriptionContextFactory(new HttpContextAccessor());
        var resolver = new SaplSubscriptionResolver(contextFactory, new ServiceCollection().BuildServiceProvider());
        return new StreamEnforceFilter(engine, resolver);
    }

    private static ActionExecutingContext CreateExecutingContext()
    {
        var method = typeof(StreamEnforceFilterTests)
            .GetMethod(nameof(NonStreamingAction), BindingFlags.NonPublic | BindingFlags.Static)!;
        var descriptor = new ControllerActionDescriptor
        {
            MethodInfo = method,
            ControllerTypeInfo = typeof(StreamEnforceFilterTests).GetTypeInfo(),
        };
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), descriptor);
        return new ActionExecutingContext(
            actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), controller: new object());
    }

    private static ActionExecutedContext CreateExecutedContext(ActionExecutingContext executing, IActionResult result) =>
        new(executing, new List<IFilterMetadata>(), controller: new object()) { Result = result };
}
