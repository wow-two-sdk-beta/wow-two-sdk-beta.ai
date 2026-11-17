import type { EvalRunStatus } from './EvalRunStatus';

/** Mirrors `EvalRunQuery` of the be contract: the query of `GET evals/runs`, newest first. */
export interface EvalRunQuery {
  readonly set?: string | null;
  /** A GUID. */
  readonly batchId?: string | null;
  readonly status?: EvalRunStatus | null;
  /** 1 to 1000; 100 when absent. */
  readonly limit?: number;
  /** The data scopes to keep; `AiConstants.unscoped` for the unscoped bucket. */
  readonly scopes?: readonly string[] | null;
}
