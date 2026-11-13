import type { AiErrorKind } from './AiErrorKind';
import type { AiOutcome } from './AiOutcome';

/** Mirrors `InferenceModel` of the be contract: one recorded model call. */
export interface InferenceModel {
  /** A GUID. */
  readonly id: string;
  /** ISO 8601 timestamp. */
  readonly atUtc: string;
  readonly useCase: string;
  readonly promptVersion: number;
  readonly promptHash: string;
  readonly promptModelMatch?: string | null;
  readonly provider: string;
  readonly model: string;
  readonly attempt: number;
  readonly subject?: string | null;
  readonly inputHash: string;
  readonly inputTokens: number;
  readonly outputTokens: number;
  readonly cacheReadTokens: number;
  readonly cacheWriteTokens: number;
  readonly reasoningTokens: number;
  readonly costMicroUsd: number;
  readonly priced: boolean;
  readonly latencyMs: number;
  readonly outcome: AiOutcome;
  readonly errorKind?: AiErrorKind | null;
  readonly error?: string | null;
  readonly finishReason?: string | null;
  readonly output?: string | null;
  /** A GUID. */
  readonly evalRunId?: string | null;
}
