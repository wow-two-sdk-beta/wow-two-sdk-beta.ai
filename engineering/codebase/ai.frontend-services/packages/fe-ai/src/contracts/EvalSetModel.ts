/** Mirrors `EvalSetModel` of the be contract: a labelled set of items a use case is scored against. */
export interface EvalSetModel {
  readonly key: string;
  readonly useCase: string;
  readonly scoreFields: readonly string[];
  readonly severeMisses: readonly string[];
  readonly note?: string | null;
  /** ISO 8601 timestamp. */
  readonly createdAtUtc: string;
  /** The data scope, such as `tenant:{id}`; absent for the unscoped bucket. Its runs take it. */
  readonly scope?: string | null;
}
