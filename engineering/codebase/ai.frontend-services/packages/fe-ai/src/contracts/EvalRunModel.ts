/** Mirrors `EvalRunModel` of the be contract: one run of an eval set against a prompt version and model. */
export interface EvalRunModel {
  /** A GUID. */
  readonly id: string;
  readonly set: string;
  readonly useCase: string;
  readonly promptVersion: number;
  readonly promptHash: string;
  readonly provider: string;
  readonly model: string;
  /** ISO 8601 timestamp. */
  readonly startedAtUtc: string;
  /** ISO 8601 timestamp; absent while the run is open. */
  readonly finishedAtUtc?: string | null;
  readonly note?: string | null;
}
