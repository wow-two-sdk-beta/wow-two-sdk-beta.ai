/** Mirrors `PromptParameters` of the be contract: the model parameters a prompt version carries. */
export interface PromptParameters {
  readonly temperature?: number | null;
  readonly maxOutputTokens?: number | null;
  readonly effort?: string | null;
}
