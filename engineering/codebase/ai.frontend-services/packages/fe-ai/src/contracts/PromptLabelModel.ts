/** Mirrors `PromptLabelModel` of the be contract: a label pointing at a prompt version. */
export interface PromptLabelModel {
  readonly useCase: string;
  readonly label: string;
  readonly version: number;
  /** ISO 8601 timestamp. */
  readonly movedAtUtc: string;
  readonly movedBy?: string | null;
}
