/** Mirrors `AiErrorKind` of the be contract; on the wire as its camelCase name. */
export type AiErrorKind =
  | 'unknown'
  | 'network'
  | 'timeout'
  | 'rateLimited'
  | 'unauthorized'
  | 'unavailable'
  | 'badRequest'
  | 'contentFiltered'
  | 'invalidResponse'
  | 'schemaMismatch'
  | 'notConfigured';
