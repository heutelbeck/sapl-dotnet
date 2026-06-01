namespace Sapl.AspNetCore.Streaming;

/// <summary>
/// An out-of-band SSE frame describing a streaming-enforcement boundary crossing or a terminal
/// denial. Serialized as {"type":"...","message":"..."} (camelCase via the shared serializer
/// options) to match the SAPL Spring, NestJS, and Python streaming demos. It is a transport
/// concern, so it lives in the host layer rather than the core enforcement model.
/// </summary>
public sealed record StreamSignalFrame(string Type, string Message);
