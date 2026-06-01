using System.Text.Json;
using Google.Protobuf;
using Sapl.Core.Authorization;
using Proto = Sapl.Rsocket.Proto;

namespace Sapl.Rsocket;

/// <summary>
/// Wire codec for the RSocket transport: encodes subscriptions to protobuf and
/// decodes the decision message types the streaming PEP consumes. The multi
/// subscription is a repeated id-to-subscription list, the multi decision a
/// keyed map, per sapl_types.proto.
/// </summary>
internal sealed class SaplProtoCodec
{
    private readonly SaplValueCodec _value = new();

    public byte[] EncodeSubscription(AuthorizationSubscription subscription) =>
        ToSubscriptionMessage(subscription).ToByteArray();

    public byte[] EncodeMultiSubscription(MultiAuthorizationSubscription subscription)
    {
        var message = new Proto.MultiAuthorizationSubscription();
        foreach (var (id, single) in subscription.Subscriptions)
        {
            message.Subscriptions.Add(new Proto.IdentifiableAuthorizationSubscription
            {
                SubscriptionId = id,
                Subscription = ToSubscriptionMessage(single),
            });
        }

        return message.ToByteArray();
    }

    public AuthorizationDecision DecodeDecision(ReadOnlySpan<byte> data) =>
        FromDecisionMessage(Proto.AuthorizationDecision.Parser.ParseFrom(data));

    public IdentifiableAuthorizationDecision DecodeIdentifiableDecision(ReadOnlySpan<byte> data)
    {
        var message = Proto.IdentifiableAuthorizationDecision.Parser.ParseFrom(data);
        return new IdentifiableAuthorizationDecision
        {
            SubscriptionId = message.SubscriptionId,
            Decision = FromDecisionMessage(message.Decision),
        };
    }

    public MultiAuthorizationDecision DecodeMultiDecision(ReadOnlySpan<byte> data)
    {
        var message = Proto.MultiAuthorizationDecision.Parser.ParseFrom(data);
        var decisions = new Dictionary<string, AuthorizationDecision>(message.Decisions.Count);
        foreach (var entry in message.Decisions)
        {
            decisions[entry.Key] = FromDecisionMessage(entry.Value);
        }

        return new MultiAuthorizationDecision { Decisions = decisions };
    }

    private Proto.AuthorizationSubscription ToSubscriptionMessage(AuthorizationSubscription subscription) =>
        new()
        {
            Subject = _value.Encode(subscription.Subject),
            Action = _value.Encode(subscription.Action),
            Resource = _value.Encode(subscription.Resource),
            Environment = _value.Encode(subscription.Environment),
            Secrets = _value.Encode(subscription.Secrets),
        };

    private AuthorizationDecision FromDecisionMessage(Proto.AuthorizationDecision message) =>
        new()
        {
            Decision = MapDecision(message.Decision),
            Obligations = DecodeArray(message.Obligations),
            Advice = DecodeArray(message.Advice),
            Resource = _value.DecodeOptional(message.Resource),
        };

    private IReadOnlyList<JsonElement>? DecodeArray(Proto.ArrayValue? array)
    {
        if (array is null || array.Elements.Count == 0)
        {
            return null;
        }

        var list = new List<JsonElement>(array.Elements.Count);
        foreach (var element in array.Elements)
        {
            list.Add(_value.DecodeToElement(element));
        }

        return list;
    }

    private static Decision MapDecision(Proto.Decision decision) => decision switch
    {
        Proto.Decision.Permit => Decision.Permit,
        Proto.Decision.Deny => Decision.Deny,
        Proto.Decision.NotApplicable => Decision.NotApplicable,
        Proto.Decision.Suspend => Decision.Suspend,
        _ => Decision.Indeterminate,
    };
}
