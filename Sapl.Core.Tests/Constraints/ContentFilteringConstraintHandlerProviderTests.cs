using System.Text.Json;
using FluentAssertions;
using Sapl.Core.Constraints.Providers;
using Sapl.Core.Pep.Constraints;
using Xunit;

namespace Sapl.Core.Tests.Constraints;

public sealed class ContentFilteringConstraintHandlerProviderTests
{
    private readonly ContentFilteringConstraintHandlerProvider _provider = new();

    private static readonly IReadOnlySet<SignalType> OutputSupported =
        new HashSet<SignalType> { SignalType.Output(typeof(object)) };

    private static JsonElement Constraint(object value) => JsonSerializer.SerializeToElement(value);

    private static JsonElement Apply(ConstraintHandler handler, object value) =>
        (JsonElement)((ConstraintHandler.Mapper)handler).Apply(value)!;

    private ScopedHandler SingleHandlerFor(object constraint) =>
        _provider.GetConstraintHandlers(Constraint(constraint), OutputSupported).Should().ContainSingle().Subject;

    [Fact]
    void UnrecognisedConstraintReturnsNoHandlers() =>
        _provider.GetConstraintHandlers(Constraint(new { type = "other" }), OutputSupported).Should().BeEmpty();

    [Fact]
    void WithoutAnOutputSignalReturnsNoHandlers() =>
        _provider.GetConstraintHandlers(
                Constraint(new { type = "filterJsonContent" }),
                new HashSet<SignalType> { SignalType.Decision })
            .Should().BeEmpty();

    [Fact]
    void HandlerScopesToTheOutputSignalAsAMapper()
    {
        var handler = SingleHandlerFor(new { type = "filterJsonContent", actions = Array.Empty<object>() });

        handler.SignalType.Kind.Should().Be(SignalKind.Output);
        handler.Handler.Should().BeOfType<ConstraintHandler.Mapper>();
    }

    [Fact]
    void BlackenActionMasksTheField()
    {
        var handler = SingleHandlerFor(new
        {
            type = "filterJsonContent",
            actions = new[] { new { type = "blacken", path = "$.ssn", discloseRight = 4, replacement = "X" } },
        });

        var result = Apply(handler.Handler, new { ssn = "123456789", name = "alice" });

        result.GetProperty("ssn").GetString().Should().Be("XXXXX6789");
        result.GetProperty("name").GetString().Should().Be("alice");
    }

    [Fact]
    void ReplaceActionSubstitutesTheField()
    {
        var handler = SingleHandlerFor(new
        {
            type = "filterJsonContent",
            actions = new[] { new { type = "replace", path = "$.secret", replacement = "***" } },
        });

        var result = Apply(handler.Handler, new { secret = "hunter2", name = "alice" });

        result.GetProperty("secret").GetString().Should().Be("***");
        result.GetProperty("name").GetString().Should().Be("alice");
    }

    [Fact]
    void DeleteActionRemovesTheField()
    {
        var handler = SingleHandlerFor(new
        {
            type = "filterJsonContent",
            actions = new[] { new { type = "delete", path = "$.secret" } },
        });

        var result = Apply(handler.Handler, new { secret = "hunter2", name = "alice" });

        result.TryGetProperty("secret", out _).Should().BeFalse();
        result.GetProperty("name").GetString().Should().Be("alice");
    }
}
