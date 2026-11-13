import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import { EvalReportView } from '../src/admin';
import type { EvalReportModel } from '../src/contracts';

const report: EvalReportModel = {
  set: 'tagging-v1',
  useCase: 'tag-post',
  runs: [
    {
      run: {
        id: 'r1',
        set: 'tagging-v1',
        useCase: 'tag-post',
        promptVersion: 3,
        promptHash: 'abc',
        provider: 'anthropic',
        model: 'haiku',
        startedAtUtc: '2026-11-13T00:00:00+00:00',
      },
      items: 2,
      answered: 2,
      correct: 1,
      accuracy: 0.5,
      severeMisses: 1,
      schemaInvalid: 0,
      failed: 0,
      fromCache: 0,
      costMicroUsd: 8200,
      costPerItemMicroUsd: 4100,
      inputTokens: 100,
      outputTokens: 20,
      cacheReadTokens: 0,
      p50LatencyMs: 850,
      p95LatencyMs: 1500,
      meanRating: 4.5,
      accuracyByTag: {},
    },
  ],
  items: [
    {
      runId: 'r1',
      itemId: 'i1',
      subject: 'post:1',
      tags: ['news'],
      expected: 'politics',
      answer: 'politics',
      correct: true,
      severe: false,
      misses: [],
      costMicroUsd: 4100,
      latencyMs: 850,
      outcome: 'Succeeded',
      rating: 5,
    },
    {
      runId: 'r1',
      itemId: 'i2',
      subject: 'post:2',
      tags: ['sport', 'live'],
      expected: 'sport',
      correct: false,
      severe: true,
      misses: ['category'],
      costMicroUsd: 4100,
      latencyMs: 1500,
      outcome: 'SchemaInvalid',
    },
  ],
};

describe('eval report view', () => {
  const wrapper = mount(EvalReportView, { props: { report } });

  it('renders a run row with accuracy, cost and latency', () => {
    const rows = wrapper.findAll('table[aria-label="Runs"] tbody tr');
    expect(rows).toHaveLength(1);
    const text = rows[0]?.text() ?? '';
    expect(text).toContain('v3');
    expect(text).toContain('anthropic/haiku');
    expect(text).toContain('50.0%');
    expect(text).toContain('$0.0041');
    expect(text).toContain('$0.0082');
    expect(text).toContain('850 ms');
    expect(text).toContain('1.50 s');
    expect(text).toContain('4.5');
  });

  it('renders an item row each', () => {
    const rows = wrapper.findAll('table[aria-label="Items"] tbody tr');
    expect(rows).toHaveLength(2);
    expect(rows[0]?.text()).toContain('politics');
    expect(rows[0]?.text()).toContain('Succeeded');
  });

  it('shows a dash for a missing answer', () => {
    const cells = wrapper.findAll('table[aria-label="Items"] tbody tr')[1]?.findAll('td') ?? [];
    expect(cells[3]?.text()).toBe('-');
    expect(cells[5]?.text()).toBe('category');
  });
});
