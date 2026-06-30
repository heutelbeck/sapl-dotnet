using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sapl.Core.Authorization;
using Sapl.Core.Client;

namespace Sapl.Core.Tests.Client;

// A compromised or malfunctioning PDP can emit a multi-decision payload that
// names the same subscription twice. Spring's MultiAuthorizationDecisionDeserializer
// tracks the seen ids and rejects the whole payload on a repeated id, so the PEP
// falls back to the deny-equivalent IndeterminateForAll. The bare id-to-decision
// map decoded here must do the same: reject fail-closed rather than last-wins-merge,
// because order would otherwise decide whether a duplicate id surfaces PERMIT.
// Traceability: DVW-11.
public class MultiDecisionDuplicateSubscriptionIdTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();

    [Fact]
    void WhenDuplicateSubscriptionIdWhereLastIsPermitThenPayloadRejectedFailClosed()
    {
        var json = """{"sub-a":{"decision":"DENY"},"sub-a":{"decision":"PERMIT"}}""";

        var result = ResponseValidator.ParseMultiDecisionFromJson(json, _logger);

        result.Should().BeNull();
    }

    [Fact]
    void WhenDuplicateSubscriptionIdWhereLastIsDenyThenPayloadRejectedFailClosed()
    {
        var json = """{"sub-a":{"decision":"PERMIT"},"sub-a":{"decision":"DENY"}}""";

        var result = ResponseValidator.ParseMultiDecisionFromJson(json, _logger);

        result.Should().BeNull();
    }

    [Fact]
    void WhenDuplicateSubscriptionIdNeverSilentlyMergesToASingleEntry()
    {
        var json = """{"sub-a":{"decision":"DENY"},"sub-a":{"decision":"PERMIT"}}""";

        var result = ResponseValidator.ParseMultiDecisionFromJson(json, _logger);

        // The fail-open delta is the duplicate collapsing into one PERMIT entry.
        // Asserting absence of any silently merged PERMIT pins the contract even
        // if the rejection signal evolves.
        result.Should().Match<MultiAuthorizationDecision?>(
            r => r == null || r.Decisions["sub-a"].Decision != Decision.Permit);
    }

    [Fact]
    void WhenAllSubscriptionIdsDistinctThenPayloadAccepted()
    {
        var json = """{"sub-a":{"decision":"PERMIT"},"sub-b":{"decision":"DENY"}}""";

        var result = ResponseValidator.ParseMultiDecisionFromJson(json, _logger);

        result.Should().NotBeNull();
        result!.Decisions.Should().HaveCount(2);
        result.Decisions["sub-a"].Decision.Should().Be(Decision.Permit);
        result.Decisions["sub-b"].Decision.Should().Be(Decision.Deny);
    }
}
