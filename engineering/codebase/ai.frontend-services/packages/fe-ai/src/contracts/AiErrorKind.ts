/** Mirrors `AiErrorKind` of the be contract; serialized as its name. */
export type AiErrorKind =
  | 'Unknown'
  | 'Network'
  | 'Timeout'
  | 'RateLimited'
  | 'Unauthorized'
  | 'Unavailable'
  | 'BadRequest'
  | 'ContentFiltered'
  | 'InvalidResponse'
  | 'SchemaMismatch'
  | 'NotConfigured';
