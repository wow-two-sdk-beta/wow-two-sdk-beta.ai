import type { AiImageRef } from './AiImageRef';

/** Mirrors `EvalItemModel` of the be contract: one labelled item of an eval set. */
export interface EvalItemModel {
  /** A GUID. */
  readonly id: string;
  readonly set: string;
  readonly subject?: string | null;
  readonly input: Readonly<Record<string, string | null>>;
  /** The images sent with the prompt, as references; never the bytes. */
  readonly images: readonly AiImageRef[];
  readonly expected: string;
  readonly tags: readonly string[];
  readonly labelledBy?: string | null;
}
