using System.Buffers;
using System.Text.Json;
using Proto = Sapl.Rsocket.Proto;

namespace Sapl.Rsocket;

/// <summary>
/// Bidirectional converter between the protobuf Value oneof and System.Text.Json
/// JsonElement, mirroring the Java and TypeScript codecs so the wire is shared.
/// SAPL undefined (a missing subscription field or an expression with no value)
/// maps to the undefined_value sentinel, distinct from JSON null.
/// </summary>
internal sealed class SaplValueCodec
{
    public Proto.Value Encode(JsonElement? element) =>
        element is null ? Undefined() : Encode(element.Value);

    public Proto.Value Encode(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                return new Proto.Value { NullValue = new Proto.NullValue() };
            case JsonValueKind.True:
            case JsonValueKind.False:
                return new Proto.Value { BoolValue = element.GetBoolean() };
            case JsonValueKind.Number:
                return new Proto.Value { NumberValue = element.GetRawText() };
            case JsonValueKind.String:
                return new Proto.Value { TextValue = element.GetString() };
            case JsonValueKind.Array:
                var array = new Proto.ArrayValue();
                foreach (var item in element.EnumerateArray())
                {
                    array.Elements.Add(Encode(item));
                }

                return new Proto.Value { ArrayValue = array };
            case JsonValueKind.Object:
                var obj = new Proto.ObjectValue();
                foreach (var property in element.EnumerateObject())
                {
                    obj.Fields[property.Name] = Encode(property.Value);
                }

                return new Proto.Value { ObjectValue = obj };
            default:
                return Undefined();
        }
    }

    /// <summary>Always produces a JsonElement; undefined and null both become JSON null.</summary>
    public JsonElement DecodeToElement(Proto.Value value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            Write(writer, value);
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    /// <summary>Returns null for the SAPL undefined or unset sentinel, else the decoded value.</summary>
    public JsonElement? DecodeOptional(Proto.Value? value)
    {
        if (value is null ||
            value.KindCase is Proto.Value.KindOneofCase.None or Proto.Value.KindOneofCase.UndefinedValue)
        {
            return null;
        }

        return DecodeToElement(value);
    }

    private void Write(Utf8JsonWriter writer, Proto.Value value)
    {
        switch (value.KindCase)
        {
            case Proto.Value.KindOneofCase.BoolValue:
                writer.WriteBooleanValue(value.BoolValue);
                break;
            case Proto.Value.KindOneofCase.NumberValue:
                writer.WriteRawValue(value.NumberValue);
                break;
            case Proto.Value.KindOneofCase.TextValue:
                writer.WriteStringValue(value.TextValue);
                break;
            case Proto.Value.KindOneofCase.ArrayValue:
                writer.WriteStartArray();
                foreach (var element in value.ArrayValue.Elements)
                {
                    Write(writer, element);
                }

                writer.WriteEndArray();
                break;
            case Proto.Value.KindOneofCase.ObjectValue:
                writer.WriteStartObject();
                foreach (var field in value.ObjectValue.Fields)
                {
                    writer.WritePropertyName(field.Key);
                    Write(writer, field.Value);
                }

                writer.WriteEndObject();
                break;
            case Proto.Value.KindOneofCase.ErrorValue:
                writer.WriteStringValue(value.ErrorValue.Message);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }

    private static Proto.Value Undefined() => new() { UndefinedValue = true };
}
