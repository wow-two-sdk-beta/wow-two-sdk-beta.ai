import type { AiImageRef } from './AiImageRef';

/** Mirrors `AiRunRequest` of the be contract: the body of `POST runs`, one call of a use case. */
export interface AiRunRequest {
  readonly useCase: string;
  /** The values for the prompt's `{{name}}` placeholders. */
  readonly variables?: Readonly<Record<string, string | null>>;
  /** The images sent with the user message, in order; a prompt takes the first `maxImages` of them. */
  readonly images?: readonly AiImageRef[];
  readonly subject?: string | null;
  readonly version?: number | null;
  readonly label?: string | null;
  /** `{provider}/{model}` references to try instead of the use case's route. */
  readonly models?: readonly string[] | null;
  readonly fresh?: boolean;
  /** Whether top-level answer values must be in their schema's `enum` lists; absent means true. */
  readonly checkEnums?: boolean;
  /** The data scope, such as `tenant:{id}`; absent for the caller's default. */
  readonly scope?: string | null;
}
