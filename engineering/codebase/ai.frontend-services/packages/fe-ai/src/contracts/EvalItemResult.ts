import type { AiOutcome } from './AiOutcome';

/** Mirrors `EvalItemResult` of the be contract: one item's result in one run. */
export interface EvalItemResult {
  /** A GUID. */
  readonly runId: string;
  /** A GUID. */
  readonly itemId: string;
  readonly subject?: string | null;
  readonly tags: readonly string[];
  readonly expected: string;
  /** The model's answer; absent unless the report was requested with answers. */
  readonly answer?: string | null;
  readonly correct: boolean;
  readonly severe: boolean;
  readonly misses: readonly string[];
  /** A GUID. */
  readonly inferenceId?: string | null;
  readonly costMicroUsd: number;
  readonly latencyMs: number;
  readonly outcome: AiOutcome;
  readonly rating?: number | null;
  readonly comment?: string | null;
}
