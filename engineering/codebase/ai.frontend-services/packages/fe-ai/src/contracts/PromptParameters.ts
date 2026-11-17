/** Mirrors `PromptParameters` of the be contract: the model parameters a prompt version carries. */
export interface PromptParameters {
  readonly temperature?: number | null;
  readonly maxOutputTokens?: number | null;
  readonly effort?: string | null;
  /** The most images the prompt sends, 0 to `AiConstants.maxImages`; absent or 0 takes text only. */
  readonly maxImages?: number | null;
}
