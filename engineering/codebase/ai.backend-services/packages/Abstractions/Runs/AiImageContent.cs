namespace WoW.Two.Sdk.Beta.Be.Ai.Runs;

/// <summary>Represents a resolved image: its bytes and their media type, as a model receives them.</summary>
/// <param name="Data">The image's bytes.</param>
/// <param name="MediaType">The media type, such as <c>image/png</c>; none when the resolver does not know it.</param>
public sealed record AiImageContent(ReadOnlyMemory<byte> Data, string? MediaType = null);
