import type { EvalRunModel } from './EvalRunModel';
import type { EvalSampleModel } from './EvalSampleModel';

/** Mirrors `EvalBatchModel` of the be contract: a queued matrix, with the items and scoring rules its cells share. */
export interface EvalBatchModel {
  /** A GUID. */
  readonly id: string;
  readonly set: string;
  readonly scope?: string | null;
  /** The pinned items (GUIDs), in the order they run. */
  readonly itemIds: readonly string[];
  readonly scoreFields: readonly string[];
  readonly severeMisses: readonly string[];
  readonly sample?: EvalSampleModel | null;
  readonly requestedBy?: string | null;
  /** ISO 8601 timestamp. */
  readonly createdAtUtc: string;
  /** One run per cell, in matrix order. */
  readonly runs: readonly EvalRunModel[];
}
