using System.Text.Json.Serialization;

namespace Sapl.Core.Authorization;

[JsonConverter(typeof(JsonStringEnumConverter<Decision>))]
public enum Decision
{
    [JsonStringEnumMemberName("PERMIT")]
    Permit,

    [JsonStringEnumMemberName("DENY")]
    Deny,

    [JsonStringEnumMemberName("INDETERMINATE")]
    Indeterminate,

    [JsonStringEnumMemberName("NOT_APPLICABLE")]
    NotApplicable,
}
