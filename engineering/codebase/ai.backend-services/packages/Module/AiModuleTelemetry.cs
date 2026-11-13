using System.Diagnostics;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module;

/// <summary>Holds the module's OpenTelemetry source: one <c>chat {model}</c> activity per model attempt, tagged with the
/// GenAI attributes (<c>gen_ai.request.model</c>, <c>gen_ai.usage.input_tokens</c>, <c>gen_ai.usage.output_tokens</c>,
/// <c>gen_ai.prompt.name</c>, <c>gen_ai.prompt.version</c>).</summary>
/// <remarks>A host exports it by adding <see cref="SourceName"/> to its tracer provider.</remarks>
public static class AiModuleTelemetry
{
    /// <summary>The activity source's name.</summary>
    public const string SourceName = "WoW.Two.Sdk.Beta.Be.Ai";

    /// <summary>Gets the activity source every attempt starts its activity from.</summary>
    internal static ActivitySource Source { get; } = new(SourceName);
}
