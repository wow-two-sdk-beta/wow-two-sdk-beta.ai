/** Mirrors `EvalSampleModel` of the be contract: which of a set's items a run takes. */
export interface EvalSampleModel {
  /** How many items to keep; absent keeps every item that passes the filters. */
  readonly count?: number | null;
  /** The seed the sample is ranked by; absent takes `AiConstants.defaultSampleSeed`. */
  readonly seed?: number | null;
  /** The tags an item must carry, all of them. */
  readonly tags: readonly string[];
  /** The items to take (GUIDs); empty takes the whole set. */
  readonly itemIds: readonly string[];
}
