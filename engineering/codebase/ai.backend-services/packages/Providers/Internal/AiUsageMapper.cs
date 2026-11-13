using Microsoft.Extensions.AI;

namespace WoW.Two.Sdk.Beta.Be.Ai.Providers.Internal;

/// <summary>Reads a response's <see cref="UsageDetails"/> into the result's token counts.</summary>
/// <remarks>
/// What each kind's SDK reports (Microsoft.Extensions.AI 10.8.3 line):
///   - anthropic (Anthropic 13.8.0): <c>InputTokenCount</c> = <c>input_tokens</c> + <c>cache_read_input_tokens</c> +
///     <c>cache_creation_input_tokens</c>; <c>CachedInputTokenCount</c> = <c>cache_read_input_tokens</c>;
///     <c>AdditionalCounts["CacheCreationInputTokens"]</c> = <c>cache_creation_input_tokens</c> (present only when
///     above 0); <c>OutputTokenCount</c> = <c>output_tokens</c>, thinking included; <c>ReasoningTokenCount</c> = the
///     thinking tokens, when reported
///   - openai (Microsoft.Extensions.AI.OpenAI 10.8.3, Chat Completions): <c>InputTokenCount</c> = <c>prompt_tokens</c>,
///     cached ones included; <c>CachedInputTokenCount</c> = <c>prompt_tokens_details.cached_tokens</c>; no cache
///     writes (OpenAI caches automatically and does not bill writes); <c>OutputTokenCount</c> = <c>completion_tokens</c>,
///     reasoning included; <c>ReasoningTokenCount</c> = <c>completion_tokens_details.reasoning_tokens</c>
///   - ollama (OllamaSharp 5.5.0): <c>InputTokenCount</c> = <c>prompt_eval_count</c>, the tokens evaluated this call
///     (a prefix reused from Ollama's KV cache is not counted and not reported); <c>OutputTokenCount</c> =
///     <c>eval_count</c>; no cache or reasoning counts
/// A client that reports input without the cached tokens (input below reads + writes) gets them added, so
/// <see cref="AiCallResult.InputTokens"/> always holds every input token.
/// </remarks>
internal static class AiUsageMapper
{
    /// <summary>The <see cref="UsageDetails.AdditionalCounts"/> key the Anthropic SDK writes cache writes under.</summary>
    internal const string CacheCreationKey = "CacheCreationInputTokens";

    /// <summary>Copies <paramref name="usage"/>'s counts onto <paramref name="result"/>.</summary>
    /// <param name="result">The result to complete.</param>
    /// <param name="usage">The response's usage; none leaves every count at 0.</param>
    internal static AiCallResult Apply(AiCallResult result, UsageDetails? usage)
    {
        if (usage is null)
            return result;

        var cacheRead = usage.CachedInputTokenCount ?? 0;
        var cacheWrite = usage.AdditionalCounts is { } counts && counts.TryGetValue(CacheCreationKey, out var written) ? written : 0;
        var input = usage.InputTokenCount ?? 0;
        if (input < cacheRead + cacheWrite)
            input += cacheRead + cacheWrite;

        return result with
        {
            InputTokens = input,
            OutputTokens = usage.OutputTokenCount ?? 0,
            CacheReadTokens = cacheRead,
            CacheWriteTokens = cacheWrite,
            ReasoningTokens = usage.ReasoningTokenCount ?? 0,
        };
    }
}
