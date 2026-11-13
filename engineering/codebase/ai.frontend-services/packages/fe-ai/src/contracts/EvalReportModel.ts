import type { EvalItemResult } from './EvalItemResult';
import type { EvalRunSummary } from './EvalRunSummary';

/** Mirrors `EvalReportModel` of the be contract: the runs of a set side by side, with each item's result. */
export interface EvalReportModel {
  readonly set: string;
  readonly useCase: string;
  readonly runs: readonly EvalRunSummary[];
  readonly items: readonly EvalItemResult[];
}
