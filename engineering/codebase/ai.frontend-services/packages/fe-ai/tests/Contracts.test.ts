import { describe, expect, it } from 'vitest';
import {
  AiConstants,
  AiRouteConstants,
  AiScopeConstants,
  evalRunStatuses,
  fillRoute,
  isEnded,
  type AiImageRef,
  type AiProviderSummary,
  type AiRunRequest,
  type EvalBatchModel,
  type EvalItemDraft,
  type EvalItemModel,
  type EvalMatrixRequest,
  type EvalRunQuery,
  type EvalRunRequest,
  type EvalSetModel,
  type InferenceModel,
  type PromptParameters,
} from '../src/contracts';

describe('contract constants', () => {
  it('mirror the be contract', () => {
    expect(AiConstants.unscoped).toBe('');
    expect(AiConstants.maxScopeLength).toBe(128);
    expect(AiConstants.defaultSampleSeed).toBe(0);
    expect(AiConstants.maxRequestedByLength).toBe(256);
    expect(Object.values(AiScopeConstants)).toEqual(['ai.read', 'ai.run', 'ai.prompts-manage', 'ai.evals-manage']);
  });

  it('keep every route under api/ai', () => {
    const routes = Object.entries(AiRouteConstants).filter(([name]) => name !== 'scopeParameter');
    expect(routes.length).toBeGreaterThan(20);
    for (const [, route] of routes) expect(route.startsWith(AiRouteConstants.root)).toBe(true);
    expect(AiRouteConstants.scopeParameter).toBe('scope');
  });

  it('fill a route, escaping each value and leaving unknown ones', () => {
    expect(fillRoute(AiRouteConstants.evalSetReport, { key: 'afisha 10' })).toBe(
      'api/ai/evals/sets/afisha%2010/report',
    );
    expect(fillRoute(AiRouteConstants.label, { key: 'k' })).toBe('api/ai/usecases/k/labels/{label}');
  });
});

describe('eval run statuses', () => {
  it('list every status and tell the ended ones', () => {
    expect(evalRunStatuses).toEqual(['queued', 'running', 'succeeded', 'failed', 'canceled']);
    expect(evalRunStatuses.filter(isEnded)).toEqual(['succeeded', 'failed', 'canceled']);
  });
});

describe('wire shapes', () => {
  it('read a queued batch as the API answers it', () => {
    const wire = JSON.parse(`{
      "id": "b1", "set": "afisha-10", "scope": "tenant:a", "itemIds": ["i1", "i2"], "scoreFields": ["relevance"],
      "severeMisses": [], "sample": {"count": 2, "seed": 5, "tags": [], "itemIds": []}, "createdAtUtc": "2026-11-17T10:00:00Z",
      "runs": [{"id": "r1", "set": "afisha-10", "useCase": "ocharo-relevance", "promptVersion": 2, "promptHash": "abcdef012345",
        "provider": "anthropic", "model": "claude-haiku-5-5", "startedAtUtc": "2026-11-17T10:00:00Z", "status": "queued",
        "itemsTotal": 2, "itemsDone": 0, "batchId": "b1", "scope": "tenant:a", "promptId": "p1"}]
    }`) as EvalBatchModel;

    expect(wire.runs[0]?.status).toBe('queued');
    expect(wire.runs[0]?.promptId).toBe('p1');
    expect(wire.sample?.count).toBe(2);
    expect(isEnded(wire.runs[0]?.status ?? 'queued')).toBe(false);
  });

  it('type the requests, scopes and catalog', () => {
    const matrix: EvalMatrixRequest = {
      models: ['anthropic/claude-haiku-5-5'],
      count: 10,
      seed: 1,
      tags: ['lang:ru'],
      requestedBy: 'max',
    };
    const now: EvalRunRequest = { set: 'afisha-10', model: 'openai/gpt-6-luna', itemIds: ['i1'] };
    const query: EvalRunQuery = { status: 'running', scopes: [AiConstants.unscoped, 'tenant:a'] };
    const set: EvalSetModel = {
      key: 'k',
      useCase: 'u',
      scoreFields: [],
      severeMisses: [],
      createdAtUtc: '2026-11-17T10:00:00Z',
      scope: 'tenant:a',
    };
    const inference: Pick<InferenceModel, 'scope' | 'outcome' | 'errorKind'> = {
      scope: null,
      outcome: 'failed',
      errorKind: 'notConfigured',
    };
    const provider: AiProviderSummary = {
      name: 'anthropic',
      configured: false,
      pricedModels: ['claude-haiku-5-5'],
      problem: 'no key',
    };

    expect([matrix.models.length, now.itemIds?.length, query.scopes?.length]).toEqual([1, 1, 2]);
    expect(set.scope).toBe('tenant:a');
    expect(inference.outcome).toBe('failed');
    expect(provider.pricedModels).toContain('claude-haiku-5-5');
  });
});

describe('image inputs', () => {
  it('mirror the image limits', () => {
    expect(AiConstants.maxImages).toBe(20);
    expect(AiConstants.maxImageUriLength).toBe(2048);
    expect(AiConstants.maxImageKeyLength).toBe(256);
    expect(AiConstants.imageMediaTypes).toEqual(['image/jpeg', 'image/png', 'image/webp', 'image/gif']);
  });

  it('type a run and an item with image references, never bytes', () => {
    const poster: AiImageRef = { key: 'fss:42', mediaType: 'image/jpeg' };
    const run: AiRunRequest = {
      useCase: 'ocharo-parse',
      variables: { caption: 'See the poster' },
      images: [poster, { uri: 'https://cdn.example.test/p.png' }],
      scope: 'tenant:a',
    };
    const draft: EvalItemDraft = { set: 'afisha-10', input: { caption: 'x' }, images: [poster], expected: '{}' };
    const parameters: PromptParameters = { maxImages: 2 };

    expect(run.images?.map((image) => image.uri ?? image.key)).toEqual(['fss:42', 'https://cdn.example.test/p.png']);
    expect(draft.images?.[0]?.key).toBe('fss:42');
    expect(parameters.maxImages).toBe(2);
  });

  it('read an item and an inference as the API answers them', () => {
    const item = JSON.parse(`{
      "id": "i1", "set": "afisha-10", "input": {"caption": "x"}, "expected": "{}", "tags": [],
      "images": [{"key": "fss:42", "sha256": "${'a'.repeat(64)}"}]
    }`) as EvalItemModel;
    const inference = JSON.parse(`{"imageCount": 1, "imageHashes": ["${'b'.repeat(64)}"]}`) as Pick<
      InferenceModel,
      'imageCount' | 'imageHashes'
    >;

    expect(item.images[0]?.key).toBe('fss:42');
    expect(item.images[0]?.sha256).toHaveLength(64);
    expect(inference.imageCount).toBe(inference.imageHashes.length);
  });
});
