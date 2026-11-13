import type { EvalRunModel } from './EvalRunModel';

/** Mirrors `EvalRunSummary` of the be contract: a run's scores, cost and latency. */
export interface EvalRunSummary {
  readonly run: EvalRunModel;
  readonly items: number;
  readonly answered: number;
  readonly correct: number;
  /** Correct over items, 0 to 1. */
  readonly accuracy: number;
  readonly severeMisses: number;
  readonly schemaInvalid: number;
  readonly failed: number;
  readonly fromCache: number;
  readonly costMicroUsd: number;
  readonly costPerItemMicroUsd: number;
  readonly inputTokens: number;
  readonly outputTokens: number;
  readonly cacheReadTokens: number;
  readonly p50LatencyMs: number;
  readonly p95LatencyMs: number;
  readonly meanRating?: number | null;
  readonly accuracyByTag: Readonly<Record<string, number>>;
}
