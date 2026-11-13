/** Mirrors `EvalItemModel` of the be contract: one labelled item of an eval set. */
export interface EvalItemModel {
  /** A GUID. */
  readonly id: string;
  readonly set: string;
  readonly subject?: string | null;
  readonly input: Readonly<Record<string, string | null>>;
  readonly expected: string;
  readonly tags: readonly string[];
  readonly labelledBy?: string | null;
}
