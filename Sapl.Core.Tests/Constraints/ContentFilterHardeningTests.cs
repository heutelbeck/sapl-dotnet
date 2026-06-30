using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Sapl.Core.Constraints.Providers;
using Sapl.Core.Pep.Constraints;
using Xunit;

namespace Sapl.Core.Tests.Constraints;

/// <summary>
/// Guards the operational hardening the Spring PEP applies to "filterJsonContent"
/// obligations: bounded blacken output, exact numeric predicate matching, a runtime
/// bound on user-supplied regular expressions, and preserving the runtime type of the
/// filtered payload. Each scenario asserts the behaviour an operator relies on to keep
/// redaction selective and the request thread safe under hostile constraints.
/// </summary>
public sealed class ContentFilterHardeningTests
{
    private static readonly IReadOnlySet<SignalType> OutputSupported =
        new HashSet<SignalType> { SignalType.Output(typeof(object)) };

    private static ConstraintHandler HandlerFor(object constraint)
    {
        var element = JsonSerializer.SerializeToElement(constraint);
        var handlers = new ContentFilteringConstraintHandlerProvider()
            .GetConstraintHandlers(element, OutputSupported);
        return handlers.Should().ContainSingle().Subject.Handler;
    }

    private static object? ApplyRaw(ConstraintHandler handler, object value) =>
        ((ConstraintHandler.Mapper)handler).Apply(value);

    private static JsonElement ApplyJson(ConstraintHandler handler, object value) =>
        (JsonElement)ApplyRaw(handler, value)!;

    /// <summary>
    /// A blacken action must not amplify a small payload into an arbitrarily large
    /// string. Spring caps the blacken length and the total masked output at one
    /// million and denies anything larger to prevent memory-exhaustion DoS (F2).
    /// </summary>
    public sealed class BlackenOutputBounds
    {
        [Fact]
        public void RejectsBlackenLengthBeyondTheOneMillionCap()
        {
            var handler = HandlerFor(new
            {
                type = "filterJsonContent",
                actions = new[]
                {
                    new { type = "blacken", path = "$.note", length = 1_000_001, replacement = "X" },
                },
            });

            var apply = () => ApplyRaw(handler, new { note = "secret" });

            apply.Should().Throw<AccessConstraintViolationException>(
                "a blacken length above the one million cap must deny rather than allocate an unbounded string");
        }

        [Fact]
        public void RejectsReplacementTimesRepetitionsBeyondTheOneMillionCap()
        {
            var handler = HandlerFor(new
            {
                type = "filterJsonContent",
                actions = new[]
                {
                    new { type = "blacken", path = "$.note", length = 600_000, replacement = "XX" },
                },
            });

            var apply = () => ApplyRaw(handler, new { note = "secret" });

            apply.Should().Throw<AccessConstraintViolationException>(
                "replacement length times repetitions above the one million cap must deny to prevent output amplification");
        }
    }

    /// <summary>
    /// Numeric predicate conditions gate which records get redacted. Spring compares
    /// the exact decimal value so integers beyond 2^53 stay distinct and selection
    /// stays precise; conflating them via double would leak unredacted data (F3).
    /// </summary>
    public sealed class NumericConditionPrecision
    {
        [Fact]
        public void SelectsOnlyTheExactlyMatchingRecordForLargeIntegers()
        {
            var handler = HandlerFor(new
            {
                type = "filterJsonContent",
                conditions = new[] { new { path = "$.id", type = "==", value = 9007199254740993L } },
                actions = new[] { new { type = "replace", path = "$.ssn", replacement = "***" } },
            });

            var payload = JsonSerializer.SerializeToElement(new object[]
            {
                new { id = 9007199254740993L, ssn = "AAA" },
                new { id = 9007199254740992L, ssn = "BBB" },
            });

            var result = ApplyJson(handler, payload);

            result[0].GetProperty("ssn").GetString().Should().Be("***");
            result[1].GetProperty("ssn").GetString().Should().Be("BBB",
                "only the record whose id exactly equals the condition is redacted; the neighbouring id must remain untouched");
        }
    }

    /// <summary>
    /// User-supplied =~ patterns must be bounded at execution time. Spring runs the
    /// match under a budget so a catastrophic-backtracking pattern denies instead of
    /// pinning the request thread; a syntactic blocklist alone is insufficient (F4).
    /// </summary>
    public sealed class RegexConditionExecutionBounds
    {
        [Fact]
        public async Task BoundsCatastrophicBacktrackingThatEvadesTheHeuristicBlocklist()
        {
            var handler = HandlerFor(new
            {
                type = "filterJsonContent",
                conditions = new[] { new { path = "$.text", type = "=~", value = "(.*a){30}z" } },
                actions = new[] { new { type = "delete", path = "$.secret" } },
            });

            var payload = new { text = new string('a', 100), secret = "s" };

            var work = Task.Run(() =>
            {
                try
                {
                    ApplyRaw(handler, payload);
                }
                catch (AccessConstraintViolationException)
                {
                    // Denying on a budget-exceeded match is the correct bounded outcome.
                }
            });

            var finished = await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(8)));

            finished.Should().BeSameAs(work,
                "a ReDoS pattern that dodges the static blocklist must still be bounded at execution time and deny");
        }
    }

    /// <summary>
    /// After applying actions, Spring converts the modified content back to the
    /// payload's original runtime type so downstream consumers still receive the
    /// declared object rather than a raw JSON element (F5).
    /// </summary>
    public sealed class FilteredContentRuntimeType
    {
        private sealed record Person(string Name, string Secret);

        [Fact]
        public void PreservesTheOriginalPayloadType()
        {
            var handler = HandlerFor(new
            {
                type = "filterJsonContent",
                actions = new[] { new { type = "delete", path = "$.secret" } },
            });

            var result = ApplyRaw(handler, new Person("alice", "hunter2"));

            result.Should().BeOfType<Person>(
                "the redacted value must round-trip back to the original runtime type, not a JsonElement");
        }

        [Fact]
        public void ReturnsRedactedContentWhenTheRuntimeTypeCannotBeReconstructed()
        {
            var handler = HandlerFor(new
            {
                type = "filterJsonContent",
                actions = new[] { new { type = "delete", path = "$.ssn" } },
            });

            // A non-materialized LINQ projection: the runtime type is a Select
            // iterator that System.Text.Json cannot deserialize back into. The
            // redaction still applies, so the redacted content must be returned
            // rather than denied -- the obligation succeeded, only the .NET type
            // reconstruction failed, which is not a policy violation.
            object payload = new[] { new { ssn = "123-45-6789", name = "Alice" } }.Select(p => p);

            var result = (JsonElement)ApplyRaw(handler, payload)!;

            result[0].TryGetProperty("ssn", out _).Should().BeFalse();
            result[0].GetProperty("name").GetString().Should().Be("Alice");
        }
    }
}
