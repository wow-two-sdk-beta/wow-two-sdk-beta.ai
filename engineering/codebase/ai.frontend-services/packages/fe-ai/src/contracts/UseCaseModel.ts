import type { PromptMode } from './PromptMode';

/** Mirrors `UseCaseModel` of the be contract: a named AI task. */
export interface UseCaseModel {
  readonly key: string;
  readonly description?: string | null;
  readonly promptMode: PromptMode;
  readonly outputSchema?: string | null;
  /** Model references (`{provider}/{model}`) tried in order. */
  readonly route: readonly string[];
  readonly keepOutputs: boolean;
  /** ISO 8601 timestamp. */
  readonly updatedAtUtc: string;
}
