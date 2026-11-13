import { AiConstants } from '../contracts';

/** Formats an integer count of micro-USD as dollars, keeping enough decimals to show sub-cent costs (`$0.0041`). */
export function formatMicroUsd(micro: number): string {
  const dollars = micro / AiConstants.microUsdPerUsd;
  const decimals = dollars === 0 || Math.abs(dollars) >= 0.01 ? 2 : 4;
  return `${dollars < 0 ? '-' : ''}$${Math.abs(dollars).toFixed(decimals)}`;
}

/** Formats a latency in milliseconds: whole milliseconds under a second, seconds with two decimals above. */
export function formatLatency(ms: number): string {
  return ms < 1000 ? `${Math.round(ms)} ms` : `${(ms / 1000).toFixed(2)} s`;
}
