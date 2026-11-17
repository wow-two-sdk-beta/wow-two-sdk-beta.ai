/** Mirrors `AiRouteConstants` of the be contract: the Ai HTTP API's routes, relative to the host's root. */
export const AiRouteConstants = {
  root: 'api/ai',
  providers: 'api/ai/providers',
  useCases: 'api/ai/usecases',
  useCase: 'api/ai/usecases/{key}',
  prompts: 'api/ai/usecases/{key}/prompts',
  promptResolve: 'api/ai/usecases/{key}/prompts/resolve',
  labels: 'api/ai/usecases/{key}/labels',
  label: 'api/ai/usecases/{key}/labels/{label}',
  prices: 'api/ai/prices',
  runs: 'api/ai/runs',
  inferences: 'api/ai/inferences',
  inferenceTotals: 'api/ai/inferences/totals',
  inferenceForget: 'api/ai/inferences/forget',
  evalSets: 'api/ai/evals/sets',
  evalSet: 'api/ai/evals/sets/{key}',
  evalSetItems: 'api/ai/evals/sets/{key}/items',
  evalSetRuns: 'api/ai/evals/sets/{key}/runs',
  evalSetReport: 'api/ai/evals/sets/{key}/report',
  evalSetExport: 'api/ai/evals/sets/{key}/export',
  evalImport: 'api/ai/evals/import',
  evalItem: 'api/ai/evals/items/{id}',
  evalItemExpected: 'api/ai/evals/items/{id}/expected',
  evalRuns: 'api/ai/evals/runs',
  evalRun: 'api/ai/evals/runs/{id}',
  evalRunCancel: 'api/ai/evals/runs/{id}/cancel',
  evalRunRatings: 'api/ai/evals/runs/{id}/ratings',
  /** The query parameter that narrows a list to data scopes, repeatable; an empty value names the unscoped bucket. */
  scopeParameter: 'scope',
} as const;

/** Fills a route's `{name}` parameters, escaping each value. */
export const fillRoute = (route: string, values: Readonly<Record<string, string>>): string =>
  route.replace(/\{(\w+)\}/g, (match, name: string) =>
    name in values ? encodeURIComponent(values[name] ?? '') : match,
  );
