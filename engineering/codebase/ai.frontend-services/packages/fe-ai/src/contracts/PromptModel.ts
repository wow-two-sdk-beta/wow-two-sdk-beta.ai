import type { PromptParameters } from './PromptParameters';

/** Mirrors `PromptModel` of the be contract: one saved prompt version of a use case. */
export interface PromptModel {
  /** A GUID. */
  readonly id: string;
  readonly useCase: string;
  readonly modelMatch?: string | null;
  readonly version: number;
  readonly systemTemplate: string;
  readonly userTemplate: string;
  readonly parameters: PromptParameters;
  readonly contentHash: string;
  readonly note?: string | null;
  readonly createdBy?: string | null;
  /** ISO 8601 timestamp. */
  readonly createdAtUtc: string;
  /** ISO 8601 timestamp. */
  readonly updatedAtUtc: string;
}
