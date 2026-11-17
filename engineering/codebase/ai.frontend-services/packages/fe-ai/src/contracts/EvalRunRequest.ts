/** Mirrors `EvalRunRequest` of the be contract: the body of `POST evals/runs`, one run now. */
export interface EvalRunRequest {
  readonly set: string;
  /** `{provider}/{model}`; the run never falls back to another. */
  readonly model: string;
  readonly version?: number | null;
  readonly label?: string | null;
  readonly fresh?: boolean;
  readonly parallelism?: number;
  readonly note?: string | null;
  readonly count?: number | null;
  readonly seed?: number | null;
  readonly tags?: readonly string[];
  /** GUIDs. */
  readonly itemIds?: readonly string[];
  readonly requestedBy?: string | null;
}
