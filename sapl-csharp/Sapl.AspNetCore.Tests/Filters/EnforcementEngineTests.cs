using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sapl.Core.Authorization;
using Sapl.Core.Client;
using Sapl.Core.Constraints;
using Sapl.Core.Constraints.Api;
using Sapl.Core.Enforcement;

namespace Sapl.AspNetCore.Tests.Filters;

public class EnforcementEngineTests
{
    private readonly IPolicyDecisionPoint _pdp = Substitute.For<IPolicyDecisionPoint>();
    private readonly ILogger<EnforcementEngine> _engineLogger = Substitute.For<ILogger<EnforcementEngine>>();
    private readonly ILogger<ConstraintEnforcementService> _serviceLogger =
        Substitute.For<ILogger<ConstraintEnforcementService>>();

    [Fact]
    async Task WhenPreEnforcePermitThenReturnsPermitted()
    {
        _pdp.DecideOnceAsync(Arg.Any<AuthorizationSubscription>(), Arg.Any<CancellationToken>())
            .Returns(AuthorizationDecision.PermitInstance);

        var engine = CreateEngine();
        var sub = AuthorizationSubscription.Create("alice", "read", "doc");

        var result = await engine.PreEnforceAsync(sub);

        result.IsPermitted.Should().BeTrue();
        result.Bundle.Should().NotBeNull();
    }

    [Fact]
    async Task WhenPreEnforceDenyThenReturnsDenied()
    {
        _pdp.DecideOnceAsync(Arg.Any<AuthorizationSubscription>(), Arg.Any<CancellationToken>())
            .Returns(AuthorizationDecision.DenyInstance);

        var engine = CreateEngine();
        var sub = AuthorizationSubscription.Create("alice", "write", "doc");

        var result = await engine.PreEnforceAsync(sub);

        result.IsPermitted.Should().BeFalse();
    }

    [Fact]
    async Task WhenPreEnforceIndeterminateThenReturnsDenied()
    {
        _pdp.DecideOnceAsync(Arg.Any<AuthorizationSubscription>(), Arg.Any<CancellationToken>())
            .Returns(AuthorizationDecision.IndeterminateInstance);

        var engine = CreateEngine();
        var sub = AuthorizationSubscription.Create("alice", "read", "doc");

        var result = await engine.PreEnforceAsync(sub);

        result.IsPermitted.Should().BeFalse();
    }

    [Fact]
    async Task WhenPreEnforceWithResourceReplacementThenPermittedWithReplacement()
    {
        var decision = new AuthorizationDecision
        {
            Decision = Decision.Permit,
            Resource = JsonDocument.Parse("""{"replaced":true}""").RootElement.Clone(),
        };
        _pdp.DecideOnceAsync(Arg.Any<AuthorizationSubscription>(), Arg.Any<CancellationToken>())
            .Returns(decision);

        var engine = CreateEngine();
        var sub = AuthorizationSubscription.Create("alice", "read", "doc");

        var result = await engine.PreEnforceAsync(sub);

        result.IsPermitted.Should().BeTrue();
        result.Bundle.Should().NotBeNull();
        result.Bundle!.HasResourceReplacement.Should().BeTrue();
    }

    [Fact]
    async Task WhenPreEnforceWithUnhandledObligationThenDenied()
    {
        var obligations = ParseArray("""[{"type":"unknownHandler"}]""");
        var decision = new AuthorizationDecision
        {
            Decision = Decision.Permit,
            Obligations = obligations,
        };
        _pdp.DecideOnceAsync(Arg.Any<AuthorizationSubscription>(), Arg.Any<CancellationToken>())
            .Returns(decision);

        var engine = CreateEngine();
        var sub = AuthorizationSubscription.Create("alice", "read", "doc");

        var result = await engine.PreEnforceAsync(sub);

        result.IsPermitted.Should().BeFalse();
    }

    [Fact]
    async Task WhenPostEnforcePermitThenReturnsValue()
    {
        _pdp.DecideOnceAsync(Arg.Any<AuthorizationSubscription>(), Arg.Any<CancellationToken>())
            .Returns(AuthorizationDecision.PermitInstance);

        var engine = CreateEngine();
        var sub = AuthorizationSubscription.Create("alice", "read", "doc");
        var value = JsonDocument.Parse("""{"name":"Jane"}""").RootElement.Clone();

        var result = await engine.PostEnforceAsync(sub, value);

        result.IsPermitted.Should().BeTrue();
    }

    [Fact]
    async Task WhenPostEnforceDenyThenReturnsDenied()
    {
        _pdp.DecideOnceAsync(Arg.Any<AuthorizationSubscription>(), Arg.Any<CancellationToken>())
            .Returns(AuthorizationDecision.DenyInstance);

        var engine = CreateEngine();
        var sub = AuthorizationSubscription.Create("alice", "read", "doc");
        var value = JsonDocument.Parse("""{"name":"Jane"}""").RootElement.Clone();

        var result = await engine.PostEnforceAsync(sub, value);

        result.IsPermitted.Should().BeFalse();
    }

    [Fact]
    async Task WhenPostEnforceWithResourceReplacementThenUsesReplacement()
    {
        var replacement = JsonDocument.Parse("""{"replaced":true}""").RootElement.Clone();
        var decision = new AuthorizationDecision
        {
            Decision = Decision.Permit,
            Resource = replacement,
        };
        _pdp.DecideOnceAsync(Arg.Any<AuthorizationSubscription>(), Arg.Any<CancellationToken>())
            .Returns(decision);

        var engine = CreateEngine();
        var sub = AuthorizationSubscription.Create("alice", "read", "doc");
        var value = JsonDocument.Parse("""{"name":"Jane"}""").RootElement.Clone();

        var result = await engine.PostEnforceAsync(sub, value);

        result.IsPermitted.Should().BeTrue();
        result.Value.GetProperty("replaced").GetBoolean().Should().BeTrue();
    }

    private EnforcementEngine CreateEngine(
        IRunnableConstraintHandlerProvider[]? runnables = null,
        IConsumerConstraintHandlerProvider[]? consumers = null,
        IMappingConstraintHandlerProvider[]? mappings = null,
        IFilterPredicateConstraintHandlerProvider[]? filters = null,
        IErrorHandlerProvider[]? errorHandlers = null,
        IErrorMappingConstraintHandlerProvider[]? errorMappings = null,
        IMethodInvocationConstraintHandlerProvider[]? methodInvocations = null)
    {
        var constraintService = new ConstraintEnforcementService(
            runnables ?? [],
            consumers ?? [],
            mappings ?? [],
            filters ?? [],
            errorHandlers ?? [],
            errorMappings ?? [],
            methodInvocations ?? [],
            _serviceLogger);

        return new EnforcementEngine(_pdp, constraintService, _engineLogger);
    }

    private static IReadOnlyList<JsonElement> ParseArray(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }
}
