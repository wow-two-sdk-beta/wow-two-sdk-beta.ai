import type { AiImageRef } from './AiImageRef';

/** Mirrors `EvalItemDraft` of the be contract: the body of `POST evals/sets/{key}/items`. */
export interface EvalItemDraft {
  readonly set: string;
  readonly subject?: string | null;
  /** The prompt variables; absent resolves them from `subject`. */
  readonly input?: Readonly<Record<string, string | null>> | null;
  /** The image references; absent takes the ones the subject source returns, else none. */
  readonly images?: readonly AiImageRef[] | null;
  /** The expected answer as a JSON object, in a string. */
  readonly expected: string;
  readonly tags?: readonly string[];
  readonly labelledBy?: string | null;
}
