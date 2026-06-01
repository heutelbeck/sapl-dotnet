using System.Text.Json;
using FluentAssertions;
using Google.Protobuf;
using Sapl.Core.Authorization;
using Proto = Sapl.Rsocket.Proto;

namespace Sapl.Rsocket.Tests;

public sealed class SaplValueCodecTests
{
    private readonly SaplValueCodec _codec = new();

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    [Fact]
    void StringRoundTrips() =>
        _codec.DecodeToElement(_codec.Encode(Json("\"alice\""))).GetString().Should().Be("alice");

    [Fact]
    void BooleanRoundTrips() =>
        _codec.DecodeToElement(_codec.Encode(Json("true"))).GetBoolean().Should().BeTrue();

    [Fact]
    void NullRoundTripsToJsonNull() =>
        _codec.DecodeToElement(_codec.Encode(Json("null"))).ValueKind.Should().Be(JsonValueKind.Null);

    [Fact]
    void HighPrecisionNumberRoundTripsViaRawText()
    {
        const string number = "123456789012345678901234567890.5";

        _codec.DecodeToElement(_codec.Encode(Json(number))).GetRawText().Should().Be(number);
    }

    [Fact]
    void ArrayRoundTripsInOrder()
    {
        var element = _codec.DecodeToElement(_codec.Encode(Json("[1,2,3]")));

        element.ValueKind.Should().Be(JsonValueKind.Array);
        element.EnumerateArray().Select(e => e.GetInt32()).Should().Equal(1, 2, 3);
    }

    [Fact]
    void NestedObjectRoundTrips() =>
        _codec.DecodeToElement(_codec.Encode(Json("""{"k":{"n":2}}""")))
            .GetProperty("k").GetProperty("n").GetRawText().Should().Be("2");

    [Fact]
    void AbsentElementEncodesToUndefinedAndDecodesToNull()
    {
        var encoded = _codec.Encode((JsonElement?)null);

        encoded.KindCase.Should().Be(Proto.Value.KindOneofCase.UndefinedValue);
        _codec.DecodeOptional(encoded).Should().BeNull();
    }
}

public sealed class SaplProtoCodecTests
{
    private readonly SaplProtoCodec _codec = new();

    private static Proto.ObjectValue Obj(string key, string value)
    {
        var obj = new Proto.ObjectValue();
        obj.Fields[key] = new Proto.Value { TextValue = value };
        return obj;
    }

    [Fact]
    void EncodeSubscriptionProducesParsableProtoWithAbsentEnvironment()
    {
        var bytes = _codec.EncodeSubscription(AuthorizationSubscription.Create("alice", "read", "doc-1"));

        var message = Proto.AuthorizationSubscription.Parser.ParseFrom(bytes);
        message.Subject.TextValue.Should().Be("alice");
        message.Action.TextValue.Should().Be("read");
        message.Resource.TextValue.Should().Be("doc-1");
        message.Environment.KindCase.Should().Be(Proto.Value.KindOneofCase.UndefinedValue);
    }

    [Fact]
    void EncodeMultiSubscriptionKeepsIdAndSubject()
    {
        var multi = new MultiAuthorizationSubscription
        {
            Subscriptions = new Dictionary<string, AuthorizationSubscription>
            {
                ["a"] = AuthorizationSubscription.Create("alice", "read", "doc-1"),
            },
        };

        var message = Proto.MultiAuthorizationSubscription.Parser.ParseFrom(_codec.EncodeMultiSubscription(multi));

        message.Subscriptions.Should().ContainSingle();
        message.Subscriptions[0].SubscriptionId.Should().Be("a");
        message.Subscriptions[0].Subscription.Subject.TextValue.Should().Be("alice");
    }

    [Fact]
    void DecodeDecisionMapsVerbAndObligations()
    {
        var message = new Proto.AuthorizationDecision
        {
            Decision = Proto.Decision.Permit,
            Obligations = new Proto.ArrayValue { Elements = { new Proto.Value { ObjectValue = Obj("type", "log") } } },
        };

        var decision = _codec.DecodeDecision(message.ToByteArray());

        decision.Decision.Should().Be(Decision.Permit);
        decision.Obligations.Should().ContainSingle();
        decision.Obligations![0].GetProperty("type").GetString().Should().Be("log");
    }

    [Fact]
    void DecodeIdentifiableDecisionMapsIdAndVerb()
    {
        var message = new Proto.IdentifiableAuthorizationDecision
        {
            SubscriptionId = "a",
            Decision = new Proto.AuthorizationDecision { Decision = Proto.Decision.Permit },
        };

        var decision = _codec.DecodeIdentifiableDecision(message.ToByteArray());

        decision.SubscriptionId.Should().Be("a");
        decision.Decision.Decision.Should().Be(Decision.Permit);
    }

    [Fact]
    void DecodeMultiDecisionMapsEveryEntry()
    {
        var message = new Proto.MultiAuthorizationDecision
        {
            Decisions =
            {
                ["a"] = new Proto.AuthorizationDecision { Decision = Proto.Decision.Permit },
                ["b"] = new Proto.AuthorizationDecision { Decision = Proto.Decision.Deny },
            },
        };

        var multi = _codec.DecodeMultiDecision(message.ToByteArray());

        multi.Decisions["a"].Decision.Should().Be(Decision.Permit);
        multi.Decisions["b"].Decision.Should().Be(Decision.Deny);
    }
}
