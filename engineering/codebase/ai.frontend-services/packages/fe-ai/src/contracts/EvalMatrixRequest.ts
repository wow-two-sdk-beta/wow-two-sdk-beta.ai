/** Mirrors `EvalMatrixRequest` of the be contract: the body of `POST evals/sets/{key}/runs`, every model × version or label. */
export interface EvalMatrixRequest {
  /** The set key; the route's key wins. */
  readonly set?: string;
  /** Each `{provider}/{model}`; at least one. */
  readonly models: readonly string[];
  readonly versions?: readonly number[];
  readonly labels?: readonly string[];
  readonly fresh?: boolean;
  /** Items sent at once per run, 1 up to the module's limit. */
  readonly parallelism?: number;
  readonly note?: string | null;
  readonly count?: number | null;
  readonly seed?: number | null;
  readonly tags?: readonly string[];
  /** GUIDs. */
  readonly itemIds?: readonly string[];
  readonly requestedBy?: string | null;
}
