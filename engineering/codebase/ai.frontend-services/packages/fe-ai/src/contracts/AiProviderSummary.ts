import type { AiErrorKind } from './AiErrorKind';

/** Mirrors `AiProviderSummary` of the be Hosting package: one provider as `GET providers` shows it, never its key. */
export interface AiProviderSummary {
  readonly name: string;
  readonly kind?: string | null;
  readonly configured: boolean;
  readonly baseUrlHost?: string | null;
  readonly problem?: string | null;
  /** The model ids with a price row. */
  readonly pricedModels: readonly string[];
  /** The model ids the provider served when asked (`?live=true`). */
  readonly liveModels?: readonly string[] | null;
  readonly liveErrorKind?: AiErrorKind | null;
  readonly liveError?: string | null;
}
