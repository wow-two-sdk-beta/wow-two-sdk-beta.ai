/** Mirrors `ModelPriceModel` of the be contract: a model's price per million tokens, in micro-USD. */
export interface ModelPriceModel {
  readonly provider: string;
  readonly model: string;
  /** ISO 8601 timestamp. */
  readonly effectiveFromUtc: string;
  readonly inputPerMTokMicroUsd: number;
  readonly outputPerMTokMicroUsd: number;
  readonly cacheWritePerMTokMicroUsd?: number | null;
  readonly cacheReadPerMTokMicroUsd?: number | null;
  readonly source?: string | null;
}
