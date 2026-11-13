import { describe, expect, it } from 'vitest';
import { formatLatency, formatMicroUsd } from '../src/kit';

describe('formatMicroUsd', () => {
  it.each([
    [4100, '$0.0041'],
    [0, '$0.00'],
    [1_500_000, '$1.50'],
    [10_000, '$0.01'],
  ])('formats %i micro-USD as %s', (micro, text) => {
    expect(formatMicroUsd(micro)).toBe(text);
  });
});

describe('formatLatency', () => {
  it.each([
    [850, '850 ms'],
    [1500, '1.50 s'],
  ])('formats %i ms as %s', (ms, text) => {
    expect(formatLatency(ms)).toBe(text);
  });
});
