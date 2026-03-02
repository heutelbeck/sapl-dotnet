using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sapl.Core.Authorization;
using Sapl.Core.Constraints;
using Sapl.Core.Constraints.Api;

namespace Sapl.Core.Tests.Constraints;

public class ConstraintEnforcementServiceTests
{
    private readonly ILogger<ConstraintEnforcementService> _logger =
        Substitute.For<ILogger<ConstraintEnforcementService>>();

    [Fact]
    void WhenObligationHandledThenBundleCreated()
    {
        var provider = CreateRunnableProvider("logAccess");
        var service = CreateService(runnables: [provider]);
        var decision = DecisionWithObligations("""[{"type":"logAccess"}]""");

        var bundle = service.PreEnforceBundleFor(decision);

        bundle.Should().NotBeNull();
        bundle.HasFailedObligations.Should().BeFalse();
    }

    [Fact]
    void WhenObligationUnhandledThenThrowsAccessDenied()
    {
        var service = CreateService();
        var decision = DecisionWithObligations("""[{"type":"unknownType"}]""");

        var act = () => service.PreEnforceBundleFor(decision);

        act.Should().Throw<AccessDeniedException>();
    }

    [Fact]
    void WhenAdviceUnhandledThenNoException()
    {
        var service = CreateService();
        var decision = DecisionWithAdvice("""[{"type":"unknownAdvice"}]""");

        var act = () => service.PreEnforceBundleFor(decision);

        act.Should().NotThrow();
    }

    [Fact]
    void WhenObligationHandlerFailsThenBundleRecordsFailure()
    {
        var provider = CreateRunnableProvider("failMe", throwOnExecute: true);
        var service = CreateService(runnables: [provider]);
        var decision = DecisionWithObligations("""[{"type":"failMe"}]""");

        var bundle = service.PreEnforceBundleFor(decision);
        bundle.HandleOnDecisionHandlers();

        bundle.HasFailedObligations.Should().BeTrue();
    }

    [Fact]
    void WhenAdviceHandlerFailsThenBundleDoesNotRecordFailure()
    {
        var provider = CreateRunnableProvider("failAdvice", throwOnExecute: true);
        var service = CreateService(runnables: [provider]);
        var decision = DecisionWithAdvice("""[{"type":"failAdvice"}]""");

        var bundle = service.PreEnforceBundleFor(decision);
        bundle.HandleOnDecisionHandlers();

        bundle.HasFailedObligations.Should().BeFalse();
    }

    [Fact]
    void WhenAllHandlersRunEvenIfFirstFailsThenNoShortCircuit()
    {
        var executionLog = new List<string>();
        var provider1 = CreateRunnableProvider("handler1", onExecute: () =>
        {
            executionLog.Add("handler1");
            throw new InvalidOperationException("fail");
        });
        var provider2 = CreateRunnableProvider("handler2", onExecute: () => executionLog.Add("handler2"));

        var obligations = ParseArray("""[{"type":"handler1"},{"type":"handler2"}]""");
        var decision = new AuthorizationDecision
        {
            Decision = Decision.Permit,
            Obligations = obligations,
        };

        var service = CreateService(runnables: [provider1, provider2]);
        var bundle = service.PreEnforceBundleFor(decision);
        bundle.HandleOnDecisionHandlers();

        executionLog.Should().Contain("handler1").And.Contain("handler2");
    }

    [Fact]
    void WhenConsumerHandlerThenReceivesValue()
    {
        object? received = null;
        var provider = CreateConsumerProvider("audit", value => received = value);
        var service = CreateService(consumers: [provider]);
        var decision = DecisionWithObligations("""[{"type":"audit"}]""");

        var bundle = service.PostEnforceBundleFor(decision);
        bundle.HandleAllOnNextConstraints("test-value");

        received.Should().Be("test-value");
    }

    [Fact]
    void WhenMappingHandlerThenTransformsValue()
    {
        var provider = CreateMappingProvider("redact", value => "REDACTED");
        var service = CreateService(mappings: [provider]);
        var decision = DecisionWithObligations("""[{"type":"redact"}]""");

        var bundle = service.PostEnforceBundleFor(decision);
        var result = bundle.HandleAllOnNextConstraints("original");

        result.Should().Be("REDACTED");
    }

    [Fact]
    void WhenFilterPredicateOnCollectionThenFiltersElements()
    {
        var provider = CreateFilterPredicateProvider("filter", value =>
            value is string s && s != "remove-me");
        var service = CreateService(filterPredicates: [provider]);
        var decision = DecisionWithObligations("""[{"type":"filter"}]""");

        var bundle = service.PostEnforceBundleFor(decision);
        var input = new List<object> { "keep", "remove-me", "also-keep" };
        var result = bundle.HandleAllOnNextConstraints(input);

        result.Should().BeAssignableTo<IEnumerable<object>>()
            .Which.Should().BeEquivalentTo(new[] { "keep", "also-keep" });
    }

    [Fact]
    void WhenErrorHandlerThenReceivesException()
    {
        Exception? received = null;
        var provider = CreateErrorHandlerProvider("notify", ex => received = ex);
        var service = CreateService(errorHandlers: [provider]);
        var decision = DecisionWithObligations("""[{"type":"notify"}]""");

        var bundle = service.PostEnforceBundleFor(decision);
        var error = new InvalidOperationException("test error");
        bundle.HandleAllOnErrorConstraints(error);

        received.Should().BeSameAs(error);
    }

    [Fact]
    void WhenErrorMappingHandlerThenTransformsException()
    {
        var provider = CreateErrorMappingProvider("enrich", ex =>
            new InvalidOperationException(ex.Message + " (enriched)", ex));
        var service = CreateService(errorMappings: [provider]);
        var decision = DecisionWithObligations("""[{"type":"enrich"}]""");

        var bundle = service.PostEnforceBundleFor(decision);
        var error = new InvalidOperationException("original");
        var result = bundle.HandleAllOnErrorConstraints(error);

        result.Message.Should().Contain("enriched");
    }

    [Fact]
    void WhenMethodInvocationHandlerThenCanModifyArgs()
    {
        var provider = CreateMethodInvocationProvider("capAmount", ctx =>
        {
            if (ctx.Args[0] is int amount && amount > 5000)
            {
                ctx.Args[0] = 5000;
            }
        });
        var service = CreateService(methodInvocations: [provider]);
        var decision = DecisionWithObligations("""[{"type":"capAmount"}]""");

        var bundle = service.PreEnforceBundleFor(decision);
        var context = new MethodInvocationContext([8000], "Transfer", "AccountService", null);
        bundle.HandleMethodInvocationHandlers(context);

        context.Args[0].Should().Be(5000);
    }

    [Fact]
    void WhenResourceReplacementThenBundleHasIt()
    {
        var service = CreateService();
        var decision = new AuthorizationDecision
        {
            Decision = Decision.Permit,
            Resource = JsonDocument.Parse("""{"replaced":true}""").RootElement.Clone(),
        };

        var bundle = service.PostEnforceBundleFor(decision);

        bundle.HasResourceReplacement.Should().BeTrue();
    }

    [Fact]
    void WhenBestEffortBundleThenUnhandledObligationsIgnored()
    {
        var service = CreateService();
        var decision = DecisionWithObligations("""[{"type":"unknownObligation"}]""");

        var act = () => service.BestEffortBundleFor(decision);

        act.Should().NotThrow();
    }

    [Fact]
    void WhenStreamingBundleThenResolvesSignalHandlers()
    {
        var onDecisionCalled = false;
        var onCompleteCalled = false;
        var provider = CreateRunnableProviderWithSignal("log", Signal.OnDecision, () => onDecisionCalled = true);
        var completeProvider = CreateRunnableProviderWithSignal("cleanup", Signal.OnComplete, () => onCompleteCalled = true);

        var service = CreateService(runnables: [provider, completeProvider]);
        var decision = DecisionWithObligations("""[{"type":"log"},{"type":"cleanup"}]""");

        var bundle = service.StreamingBundleFor(decision);
        bundle.HandleOnDecisionHandlers();
        bundle.HandleOnCompleteConstraints();

        onDecisionCalled.Should().BeTrue();
        onCompleteCalled.Should().BeTrue();
    }

    private static AuthorizationDecision DecisionWithObligations(string json) =>
        new()
        {
            Decision = Decision.Permit,
            Obligations = ParseArray(json),
        };

    private static AuthorizationDecision DecisionWithAdvice(string json) =>
        new()
        {
            Decision = Decision.Permit,
            Advice = ParseArray(json),
        };

    private static IReadOnlyList<JsonElement> ParseArray(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private ConstraintEnforcementService CreateService(
        IRunnableConstraintHandlerProvider[]? runnables = null,
        IConsumerConstraintHandlerProvider[]? consumers = null,
        IMappingConstraintHandlerProvider[]? mappings = null,
        IFilterPredicateConstraintHandlerProvider[]? filterPredicates = null,
        IErrorHandlerProvider[]? errorHandlers = null,
        IErrorMappingConstraintHandlerProvider[]? errorMappings = null,
        IMethodInvocationConstraintHandlerProvider[]? methodInvocations = null) =>
        new(
            runnables ?? [],
            consumers ?? [],
            mappings ?? [],
            filterPredicates ?? [],
            errorHandlers ?? [],
            errorMappings ?? [],
            methodInvocations ?? [],
            _logger);

    private static bool MatchesType(JsonElement e, string type) =>
        e.ValueKind == JsonValueKind.Object &&
        e.TryGetProperty("type", out var t) &&
        t.GetString() == type;

    private static IRunnableConstraintHandlerProvider CreateRunnableProvider(
        string type, bool throwOnExecute = false, Action? onExecute = null)
    {
        var provider = Substitute.For<IRunnableConstraintHandlerProvider>();
        provider.IsResponsible(Arg.Any<JsonElement>())
            .Returns(ci => MatchesType(ci.Arg<JsonElement>(), type));
        provider.Signal.Returns(Signal.OnDecision);
        provider.GetHandler(Arg.Any<JsonElement>()).Returns(_ =>
        {
            return (Action)(() =>
            {
                onExecute?.Invoke();
                if (throwOnExecute)
                    throw new InvalidOperationException("handler failed");
            });
        });
        return provider;
    }

    private static IRunnableConstraintHandlerProvider CreateRunnableProviderWithSignal(
        string type, Signal signal, Action onExecute)
    {
        var provider = Substitute.For<IRunnableConstraintHandlerProvider>();
        provider.IsResponsible(Arg.Any<JsonElement>())
            .Returns(ci => MatchesType(ci.Arg<JsonElement>(), type));
        provider.Signal.Returns(signal);
        provider.GetHandler(Arg.Any<JsonElement>()).Returns(_ => onExecute);
        return provider;
    }

    private static IConsumerConstraintHandlerProvider CreateConsumerProvider(
        string type, Action<object> onConsume)
    {
        var provider = Substitute.For<IConsumerConstraintHandlerProvider>();
        provider.IsResponsible(Arg.Any<JsonElement>())
            .Returns(ci => MatchesType(ci.Arg<JsonElement>(), type));
        provider.GetHandler(Arg.Any<JsonElement>()).Returns(_ => onConsume);
        return provider;
    }

    private static IMappingConstraintHandlerProvider CreateMappingProvider(
        string type, Func<object, object> mapper)
    {
        var provider = Substitute.For<IMappingConstraintHandlerProvider>();
        provider.IsResponsible(Arg.Any<JsonElement>())
            .Returns(ci => MatchesType(ci.Arg<JsonElement>(), type));
        provider.Priority.Returns(0);
        provider.GetHandler(Arg.Any<JsonElement>()).Returns(_ => mapper);
        return provider;
    }

    private static IFilterPredicateConstraintHandlerProvider CreateFilterPredicateProvider(
        string type, Func<object, bool> predicate)
    {
        var provider = Substitute.For<IFilterPredicateConstraintHandlerProvider>();
        provider.IsResponsible(Arg.Any<JsonElement>())
            .Returns(ci => MatchesType(ci.Arg<JsonElement>(), type));
        provider.GetHandler(Arg.Any<JsonElement>()).Returns(_ => predicate);
        return provider;
    }

    private static IErrorHandlerProvider CreateErrorHandlerProvider(
        string type, Action<Exception> handler)
    {
        var provider = Substitute.For<IErrorHandlerProvider>();
        provider.IsResponsible(Arg.Any<JsonElement>())
            .Returns(ci => MatchesType(ci.Arg<JsonElement>(), type));
        provider.GetHandler(Arg.Any<JsonElement>()).Returns(_ => handler);
        return provider;
    }

    private static IErrorMappingConstraintHandlerProvider CreateErrorMappingProvider(
        string type, Func<Exception, Exception> mapper)
    {
        var provider = Substitute.For<IErrorMappingConstraintHandlerProvider>();
        provider.IsResponsible(Arg.Any<JsonElement>())
            .Returns(ci => MatchesType(ci.Arg<JsonElement>(), type));
        provider.Priority.Returns(0);
        provider.GetHandler(Arg.Any<JsonElement>()).Returns(_ => mapper);
        return provider;
    }

    private static IMethodInvocationConstraintHandlerProvider CreateMethodInvocationProvider(
        string type, Action<MethodInvocationContext> handler)
    {
        var provider = Substitute.For<IMethodInvocationConstraintHandlerProvider>();
        provider.IsResponsible(Arg.Any<JsonElement>())
            .Returns(ci => MatchesType(ci.Arg<JsonElement>(), type));
        provider.GetHandler(Arg.Any<JsonElement>()).Returns(_ => handler);
        return provider;
    }
}
