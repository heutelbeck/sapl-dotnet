using System.Runtime.CompilerServices;
using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sapl.Core.Authorization;
using Sapl.Core.Client;
using Sapl.Core.Constraints;
using Sapl.Core.Constraints.Api;
using Sapl.Core.Enforcement;

namespace Sapl.Core.Tests.Enforcement;

public class StreamingEnforcementCoreTests
{
    private readonly IPolicyDecisionPoint _pdp = Substitute.For<IPolicyDecisionPoint>();
    private readonly ILogger<EnforcementEngine> _engineLogger = Substitute.For<ILogger<EnforcementEngine>>();
    private readonly ILogger<ConstraintEnforcementService> _serviceLogger =
        Substitute.For<ILogger<ConstraintEnforcementService>>();

    [Fact]
    async Task WhenTillDeniedPermitThenEmitsItems()
    {
        SetupDecisionStream(AuthorizationDecision.PermitInstance);

        var engine = CreateEngine();
        var sub = AuthorizationSubscription.Create("alice", "read", "stream");
        var items = new List<int>();

        await foreach (var item in engine.EnforceTillDenied(sub, () => GenerateItems(1, 2, 3)))
        {
            items.Add(item);
        }

        items.Should().Equal(1, 2, 3);
    }

    [Fact]
    async Task WhenTillDeniedDenyThenThrowsAccessDenied()
    {
        SetupDecisionStream(AuthorizationDecision.DenyInstance);

        var engine = CreateEngine();
        var sub = AuthorizationSubscription.Create("alice", "read", "stream");
        var items = new List<int>();

        var act = async () =>
        {
            await foreach (var item in engine.EnforceTillDenied(sub, () => GenerateItems(1, 2, 3)))
            {
                items.Add(item);
            }
        };

        await act.Should().ThrowAsync<AccessDeniedException>();
        items.Should().BeEmpty();
    }

    [Fact]
    async Task WhenTillDeniedPermitThenDenyThenStopsStream()
    {
        var decisionChannel = Channel.CreateUnbounded<AuthorizationDecision>();
        SetupDecisionStreamFromChannel(decisionChannel.Reader);

        var engine = CreateEngine();
        var sub = AuthorizationSubscription.Create("alice", "read", "stream");
        var sourceChannel = Channel.CreateUnbounded<int>();
        var items = new List<int>();
        var denied = false;

        await decisionChannel.Writer.WriteAsync(AuthorizationDecision.PermitInstance);
        await sourceChannel.Writer.WriteAsync(1);
        await sourceChannel.Writer.WriteAsync(2);

        var act = async () =>
        {
            await foreach (var item in engine.EnforceTillDenied(
                sub, () => ReadChannel(sourceChannel.Reader),
                onDeny: _ => denied = true))
            {
                items.Add(item);
                if (items.Count == 2)
                {
                    await decisionChannel.Writer.WriteAsync(AuthorizationDecision.DenyInstance);
                    decisionChannel.Writer.Complete();
                }
            }
        };

        await act.Should().ThrowAsync<AccessDeniedException>();
        items.Should().Equal(1, 2);
        denied.Should().BeTrue();
    }

    [Fact]
    async Task WhenDropWhileDeniedThenDropsDuringDeny()
    {
        var decisionChannel = Channel.CreateUnbounded<AuthorizationDecision>();
        SetupDecisionStreamFromChannel(decisionChannel.Reader);

        var engine = CreateEngine();
        var sub = AuthorizationSubscription.Create("alice", "read", "stream");
        var sourceChannel = Channel.CreateUnbounded<int>();
        var items = new List<int>();

        await decisionChannel.Writer.WriteAsync(AuthorizationDecision.PermitInstance);
        await sourceChannel.Writer.WriteAsync(1);

        var cts = new CancellationTokenSource();

        var task = Task.Run(async () =>
        {
            await foreach (var item in engine.EnforceDropWhileDenied(sub,
                () => ReadChannel(sourceChannel.Reader), cts.Token))
            {
                items.Add(item);
                if (items.Count == 1)
                {
                    await decisionChannel.Writer.WriteAsync(AuthorizationDecision.DenyInstance);
                    await Task.Delay(300);
                    await sourceChannel.Writer.WriteAsync(99);
                    await Task.Delay(200);
                    await decisionChannel.Writer.WriteAsync(AuthorizationDecision.PermitInstance);
                    await Task.Delay(200);
                    await sourceChannel.Writer.WriteAsync(2);
                }
                if (items.Count == 2)
                {
                    sourceChannel.Writer.Complete();
                    decisionChannel.Writer.Complete();
                }
            }
        });

        await task.WaitAsync(TimeSpan.FromSeconds(10));
        items.Should().Equal(1, 2);
    }

    [Fact]
    async Task WhenRecoverableIfDeniedThenSignalsTransitions()
    {
        var decisionChannel = Channel.CreateUnbounded<AuthorizationDecision>();
        SetupDecisionStreamFromChannel(decisionChannel.Reader);

        var engine = CreateEngine();
        var sub = AuthorizationSubscription.Create("alice", "read", "stream");
        var sourceChannel = Channel.CreateUnbounded<int>();
        var items = new List<int>();
        var denyCount = 0;
        var recoverCount = 0;

        await decisionChannel.Writer.WriteAsync(AuthorizationDecision.PermitInstance);
        await sourceChannel.Writer.WriteAsync(1);

        var task = Task.Run(async () =>
        {
            await foreach (var item in engine.EnforceRecoverableIfDenied(
                sub, () => ReadChannel(sourceChannel.Reader),
                onDeny: _ => Interlocked.Increment(ref denyCount),
                onRecover: _ => Interlocked.Increment(ref recoverCount)))
            {
                items.Add(item);
                if (items.Count == 1)
                {
                    await decisionChannel.Writer.WriteAsync(AuthorizationDecision.DenyInstance);
                    await Task.Delay(300);
                    await decisionChannel.Writer.WriteAsync(AuthorizationDecision.PermitInstance);
                    await Task.Delay(200);
                    await sourceChannel.Writer.WriteAsync(2);
                }
                if (items.Count == 2)
                {
                    sourceChannel.Writer.Complete();
                    decisionChannel.Writer.Complete();
                }
            }
        });

        await task.WaitAsync(TimeSpan.FromSeconds(10));
        items.Should().Equal(1, 2);
        denyCount.Should().Be(1);
        recoverCount.Should().Be(1);
    }

    [Fact]
    async Task WhenNoPermitReceivedThenSourceNotSubscribed()
    {
        var decisionChannel = Channel.CreateUnbounded<AuthorizationDecision>();
        SetupDecisionStreamFromChannel(decisionChannel.Reader);

        var engine = CreateEngine();
        var sub = AuthorizationSubscription.Create("alice", "read", "stream");
        var sourceInvoked = false;

        await decisionChannel.Writer.WriteAsync(AuthorizationDecision.IndeterminateInstance);
        decisionChannel.Writer.Complete();

        var act = async () =>
        {
            await foreach (var _ in engine.EnforceDropWhileDenied(sub, () =>
            {
                sourceInvoked = true;
                return GenerateItems(1);
            }))
            {
                // Should not reach here
            }
        };

        await act.Should().NotThrowAsync();
        sourceInvoked.Should().BeFalse();
    }

    [Fact]
    async Task WhenTillDeniedWithUnhandledObligationThenDenied()
    {
        var decision = new AuthorizationDecision
        {
            Decision = Decision.Permit,
            Obligations = ParseArray("""[{"type":"unknownHandler"}]"""),
        };
        SetupDecisionStream(decision);

        var engine = CreateEngine();
        var sub = AuthorizationSubscription.Create("alice", "read", "stream");

        var act = async () =>
        {
            await foreach (var _ in engine.EnforceTillDenied(sub, () => GenerateItems(1, 2, 3)))
            {
            }
        };

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    async Task WhenCancelledThenStopsCleanly()
    {
        SetupDecisionStream(AuthorizationDecision.PermitInstance);

        var engine = CreateEngine();
        var sub = AuthorizationSubscription.Create("alice", "read", "stream");
        var cts = new CancellationTokenSource();
        var items = new List<int>();

        var act = async () =>
        {
            await foreach (var item in engine.EnforceTillDenied(sub,
                () => GenerateInfiniteItems(), cancellationToken: cts.Token))
            {
                items.Add(item);
                if (items.Count == 3)
                {
                    cts.Cancel();
                }
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
        items.Count.Should().BeGreaterThanOrEqualTo(3);
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

    private void SetupDecisionStream(params AuthorizationDecision[] decisions)
    {
        _pdp.Decide(Arg.Any<AuthorizationSubscription>(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(decisions));
    }

    private static async IAsyncEnumerable<AuthorizationDecision> ToAsyncEnumerable(
        AuthorizationDecision[] items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }

    private void SetupDecisionStreamFromChannel(ChannelReader<AuthorizationDecision> reader)
    {
        _pdp.Decide(Arg.Any<AuthorizationSubscription>(), Arg.Any<CancellationToken>())
            .Returns(reader.ReadAllAsync());
    }

    private static async IAsyncEnumerable<int> GenerateItems(params int[] values)
    {
        foreach (var value in values)
        {
            yield return value;
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<int> GenerateInfiniteItems(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var i = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            yield return i++;
            await Task.Delay(10, cancellationToken);
        }
    }

    private static async IAsyncEnumerable<int> ReadChannel(
        ChannelReader<int> reader,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in reader.ReadAllAsync(cancellationToken))
        {
            yield return item;
        }
    }

    private static IReadOnlyList<System.Text.Json.JsonElement> ParseArray(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }
}
