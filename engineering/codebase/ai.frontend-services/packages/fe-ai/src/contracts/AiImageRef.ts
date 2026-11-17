/**
 * Mirrors `AiImageRef` of the be contract: one image a call sends with its prompt, as a reference the service resolves.
 * Give exactly one of `uri` and `key`; a reference never carries the bytes.
 */
export interface AiImageRef {
  /** An absolute `http` or `https` URI the service fetches. */
  readonly uri?: string | null;
  /** A stable key the host resolves, such as `fss:{id}` for an image in SCFD's file storage. */
  readonly key?: string | null;
  /** The media type, such as `image/jpeg`, when known. */
  readonly mediaType?: string | null;
  /** The SHA-256 of the bytes, 64 hex characters, when known; a resolved image that differs is refused. */
  readonly sha256?: string | null;
}
