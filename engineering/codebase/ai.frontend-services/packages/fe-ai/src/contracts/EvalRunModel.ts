import type { EvalRunStatus } from './EvalRunStatus';
import type { EvalSampleModel } from './EvalSampleModel';

/** Mirrors `EvalRunModel` of the be contract: one run of an eval set against a pinned prompt and a model, and where it stands. */
export interface EvalRunModel {
  /** A GUID. */
  readonly id: string;
  readonly set: string;
  readonly useCase: string;
  /** The prompt version, pinned when the run was queued. */
  readonly promptVersion: number;
  /** The pinned prompt's content hash. */
  readonly promptHash: string;
  readonly provider: string;
  readonly model: string;
  /** ISO 8601 timestamp; while queued, when it was queued. */
  readonly startedAtUtc: string;
  /** ISO 8601 timestamp; absent while the run is queued or running. */
  readonly finishedAtUtc?: string | null;
  readonly note?: string | null;
  /** A run made before the queue reads `succeeded`. */
  readonly status: EvalRunStatus;
  readonly itemsTotal: number;
  readonly itemsDone: number;
  /** Why the run failed, when it did. */
  readonly error?: string | null;
  /** A GUID, shared by every cell of one matrix. */
  readonly batchId?: string | null;
  readonly requestedBy?: string | null;
  /** ISO 8601 timestamp of the last heartbeat. */
  readonly heartbeatAtUtc?: string | null;
  readonly sample?: EvalSampleModel | null;
  /** The data scope, such as `tenant:{id}`; absent for the unscoped bucket. */
  readonly scope?: string | null;
  /** A GUID: the prompt row the run pinned; absent for a run made before pinning. */
  readonly promptId?: string | null;
}
