/** Mirrors `EvalRunStatus` of the be contract: where an eval run stands; on the wire as its camelCase name. */
export type EvalRunStatus = 'queued' | 'running' | 'succeeded' | 'failed' | 'canceled';

/** Every status, in the order a run moves through them. */
export const evalRunStatuses: readonly EvalRunStatus[] = ['queued', 'running', 'succeeded', 'failed', 'canceled'];

/** Whether a run has ended: succeeded, failed or canceled. */
export const isEnded = (status: EvalRunStatus): boolean =>
  status === 'succeeded' || status === 'failed' || status === 'canceled';
