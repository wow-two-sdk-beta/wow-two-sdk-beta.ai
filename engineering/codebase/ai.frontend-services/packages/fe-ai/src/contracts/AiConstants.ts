/** Mirrors `AiConstants` of the be contract: the limits and well-known values every form shares. */
export const AiConstants = {
  /** The longest use case key: lowercase letters, digits and hyphens. */
  maxKeyLength: 64,
  /** The longest model reference, `{provider}/{model}`. */
  maxModelRefLength: 128,
  /** The longest subject reference. */
  maxSubjectLength: 256,
  /** The label a versioned use case resolves when a caller names neither a version nor a label. */
  productionLabel: 'production',
  /** The label for the version under evaluation. */
  candidateLabel: 'candidate',
  /** Micro-USD in one US dollar; every cost in the contract is an integer count of micro-USD. */
  microUsdPerUsd: 1_000_000,
  /** The seed an eval run's sample is ranked by when the request names none. */
  defaultSampleSeed: 0,
  /** The longest name of whoever requested an eval run. */
  maxRequestedByLength: 256,
  /** The longest data scope, such as `tenant:{id}`. */
  maxScopeLength: 128,
  /** The data scope of calls, sets and runs made without one; a filter names it to keep only those. */
  unscoped: '',
} as const;
