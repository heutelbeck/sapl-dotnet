using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sapl.AspNetCore.Enforcement;
using Sapl.AspNetCore.Filters;
using Sapl.AspNetCore.Interception;
using Sapl.Core.Attributes;
using Sapl.Core.Authorization;
using Sapl.Core.Client;
using Sapl.Core.Constraints;
using Sapl.Core.Pep.Constraints;
using Sapl.Core.Pep.Enforcement;

namespace Sapl.AspNetCore.Tests.Filters;

/// <summary>
/// After a PERMIT, the protected action runs and its result must always pass through output
/// enforcement, mirroring the Spring blocking @PreEnforce PEP which invokes
/// enforceOutputConstraints(returnedObject, false) on every returned object including null.
/// These scenarios pin that an output obligation is honoured regardless of the result shape,
/// so a void/empty action or a null payload cannot silently fail open. (BP-PRE-OUTPUT-SKIP)
/// </summary>
public sealed class PreEnforceFilterOutputEnforcementTests
{
    private const string FailingObligation = "fail-output";
    private const string RecordingObligation = "audit-output";

    [Theory]
    [InlineData(ResultShape.NullValuedObjectResult)]
    [InlineData(ResultShape.NoContentResult)]
    [InlineData(ResultShape.EmptyResult)]
    async Task WhenPermitOutputObligationFailsAndActionYieldsNoPayloadThenAccessDenied(ResultShape shape)
    {
        var act = () => RunFilterAsync(
            PermitWith(FailingObligation),
            new ThrowingOutputProvider(FailingObligation),
            ResultFor(shape));

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Theory]
    [InlineData(ResultShape.NullValuedObjectResult)]
    [InlineData(ResultShape.NoContentResult)]
    [InlineData(ResultShape.EmptyResult)]
    async Task WhenPermitCarriesOutputObligationAndActionYieldsNoPayloadThenHandlerStillRuns(ResultShape shape)
    {
        var ran = false;

        await RunFilterAsync(
            PermitWith(RecordingObligation),
            new RecordingOutputProvider(RecordingObligation, () => ran = true),
            ResultFor(shape));

        ran.Should().BeTrue();
    }

    [Fact]
    async Task WhenPermitOutputObligationFailsAndActionYieldsPayloadThenAccessDenied()
    {
        var act = () => RunFilterAsync(
            PermitWith(FailingObligation),
            new ThrowingOutputProvider(FailingObligation),
            new ObjectResult("payload"));

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    private static async Task RunFilterAsync(
        AuthorizationDecision decision, IConstraintHandlerProvider provider, IActionResult result)
    {
        var httpContext = new DefaultHttpContext();
        var resolver = new SaplSubscriptionResolver(
            new HttpSubscriptionContextFactory(new HttpContextAccessor { HttpContext = httpContext }),
            new ServiceCollection().BuildServiceProvider());
        var engine = new EnforcementEngine(new StubPdp(decision), [provider]);
        var filter = new PreEnforceFilter(engine, resolver);

        var method = typeof(GuardedActions).GetMethod(nameof(GuardedActions.ReturnsPayload))!;
        var descriptor = new ControllerActionDescriptor
        {
            MethodInfo = method,
            ControllerTypeInfo = typeof(GuardedActions).GetTypeInfo(),
            ControllerName = "Guarded",
            ActionName = method.Name,
        };
        var actionContext = new ActionContext(httpContext, new RouteData(), descriptor);
        var filters = new List<IFilterMetadata>();
        var controller = new GuardedActions();
        var executing = new ActionExecutingContext(
            actionContext, filters, new Dictionary<string, object?>(), controller);
        var executed = new ActionExecutedContext(actionContext, filters, controller) { Result = result };

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
    }

    private static IActionResult ResultFor(ResultShape shape) => shape switch
    {
        ResultShape.NullValuedObjectResult => new ObjectResult(null),
        ResultShape.NoContentResult => new NoContentResult(),
        ResultShape.EmptyResult => new EmptyResult(),
        _ => throw new ArgumentOutOfRangeException(nameof(shape)),
    };

    private static AuthorizationDecision PermitWith(string obligationType) => new()
    {
        Decision = Decision.Permit,
        Obligations = [JsonSerializer.SerializeToElement(new { type = obligationType })],
    };

    public enum ResultShape
    {
        NullValuedObjectResult,
        NoContentResult,
        EmptyResult,
    }

    private sealed class GuardedActions
    {
        [PreEnforce]
        public string? ReturnsPayload() => null;
    }

    private static ScopedHandler OutputHandler(
        ConstraintHandler handler, IReadOnlySet<SignalType> supportedSignals) =>
        new(handler, supportedSignals.First(signal => signal.Kind == SignalKind.Output), 0);

    private sealed class ThrowingOutputProvider(string type) : IConstraintHandlerProvider
    {
        public IReadOnlyList<ScopedHandler> GetConstraintHandlers(
            JsonElement constraint, IReadOnlySet<SignalType> supportedSignals) =>
            IConstraintHandlerProvider.ConstraintIsOfType(constraint, type)
                ? [OutputHandler(
                    new ConstraintHandler.Runner(() => throw new InvalidOperationException("output obligation failed")),
                    supportedSignals)]
                : [];
    }

    private sealed class RecordingOutputProvider(string type, Action onRun) : IConstraintHandlerProvider
    {
        public IReadOnlyList<ScopedHandler> GetConstraintHandlers(
            JsonElement constraint, IReadOnlySet<SignalType> supportedSignals) =>
            IConstraintHandlerProvider.ConstraintIsOfType(constraint, type)
                ? [OutputHandler(new ConstraintHandler.Runner(onRun), supportedSignals)]
                : [];
    }

    private sealed class StubPdp(AuthorizationDecision decision) : IPolicyDecisionPoint
    {
        public Task<AuthorizationDecision> DecideOnceAsync(AuthorizationSubscription s, CancellationToken c = default) =>
            Task.FromResult(decision);

        public async IAsyncEnumerable<AuthorizationDecision> Decide(
            AuthorizationSubscription s,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken c = default)
        {
            yield return decision;
            await Task.CompletedTask;
        }

        public Task<MultiAuthorizationDecision> MultiDecideAllOnceAsync(MultiAuthorizationSubscription s, CancellationToken c = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<IdentifiableAuthorizationDecision> MultiDecide(MultiAuthorizationSubscription s, CancellationToken c = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<MultiAuthorizationDecision> MultiDecideAll(MultiAuthorizationSubscription s, CancellationToken c = default) =>
            throw new NotSupportedException();
    }
}
