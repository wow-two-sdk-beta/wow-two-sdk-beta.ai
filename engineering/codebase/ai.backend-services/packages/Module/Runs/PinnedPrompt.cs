using WoW.Two.Sdk.Beta.Be.Ai.Prompts;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Runs;

/// <summary>Holds the prompt a queued eval run pinned: its row as it was then, and the use case's output schema then.</summary>
/// <param name="Prompt">The row: id, version, model match, templates, parameters and content hash.</param>
/// <param name="OutputSchema">The output schema the answer must match; none takes free text.</param>
internal sealed record PinnedPrompt(PromptModel Prompt, string? OutputSchema);
