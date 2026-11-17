/** Mirrors `AiScopeConstants` of the be contract: the OAuth scopes the Ai HTTP API accepts. */
export const AiScopeConstants = {
  /** Reads providers, use cases, prompts, labels, prices, inferences, totals, eval sets, runs and reports. */
  read: 'ai.read',
  /** Runs a use case and purges a subject's stored answers. */
  run: 'ai.run',
  /** Writes use cases, prompts, labels and prices. */
  promptsManage: 'ai.prompts-manage',
  /** Writes eval sets and items, reads their inputs, queues, cancels and rates runs, exports and imports sets. */
  evalsManage: 'ai.evals-manage',
} as const;

/** One of the API's scopes. */
export type AiScope = (typeof AiScopeConstants)[keyof typeof AiScopeConstants];
