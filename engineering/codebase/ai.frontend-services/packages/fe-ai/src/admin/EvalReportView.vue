<script setup lang="ts">
import { Badge, BadgeVariant, Card } from '@wow-two-beta/fe/presentation/display';
import type { AiOutcome, EvalReportModel } from '../contracts';
import { formatLatency, formatMicroUsd } from '../kit';

/** Renders an eval report read-only: one row per run, then one row per item result. Fetching is the caller's job. */
defineOptions({ name: 'EvalReportView' });

defineProps<{
  /** The report to show. */
  report: EvalReportModel;
}>();

const percent = (value: number) => `${(value * 100).toFixed(1)}%`;
const outcomeVariant = (outcome: AiOutcome) => (outcome === 'Succeeded' ? BadgeVariant.Success : BadgeVariant.Danger);
</script>

<template>
  <div class="flex flex-col gap-6">
    <h2 class="text-lg font-semibold">
      {{ report.set }} <span class="text-sm font-normal text-muted-foreground">{{ report.useCase }}</span>
    </h2>

    <Card class="overflow-x-auto p-4">
      <table aria-label="Runs" class="w-full text-left text-sm">
        <thead class="text-muted-foreground">
          <tr>
            <th class="pr-4">Prompt</th>
            <th class="pr-4">Model</th>
            <th class="pr-4">Accuracy</th>
            <th class="pr-4">Severe misses</th>
            <th class="pr-4">Failed</th>
            <th class="pr-4">Cost / item</th>
            <th class="pr-4">Total cost</th>
            <th class="pr-4">p50</th>
            <th class="pr-4">p95</th>
            <th>Mean rating</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="summary in report.runs" :key="summary.run.id">
            <td class="pr-4">v{{ summary.run.promptVersion }}</td>
            <td class="pr-4">{{ summary.run.provider }}/{{ summary.run.model }}</td>
            <td class="pr-4">{{ percent(summary.accuracy) }}</td>
            <td class="pr-4">{{ summary.severeMisses }}</td>
            <td class="pr-4">{{ summary.failed }}</td>
            <td class="pr-4">{{ formatMicroUsd(summary.costPerItemMicroUsd) }}</td>
            <td class="pr-4">{{ formatMicroUsd(summary.costMicroUsd) }}</td>
            <td class="pr-4">{{ formatLatency(summary.p50LatencyMs) }}</td>
            <td class="pr-4">{{ formatLatency(summary.p95LatencyMs) }}</td>
            <td>{{ summary.meanRating == null ? '-' : summary.meanRating.toFixed(1) }}</td>
          </tr>
        </tbody>
      </table>
    </Card>

    <Card class="overflow-x-auto p-4">
      <table aria-label="Items" class="w-full text-left text-sm">
        <thead class="text-muted-foreground">
          <tr>
            <th class="pr-4">Subject</th>
            <th class="pr-4">Tags</th>
            <th class="pr-4">Expected</th>
            <th class="pr-4">Answer</th>
            <th class="pr-4">Correct</th>
            <th class="pr-4">Misses</th>
            <th class="pr-4">Cost</th>
            <th class="pr-4">Latency</th>
            <th class="pr-4">Outcome</th>
            <th>Rating</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in report.items" :key="`${item.runId}:${item.itemId}`">
            <td class="pr-4">{{ item.subject ?? '-' }}</td>
            <td class="pr-4">{{ item.tags.join(', ') }}</td>
            <td class="pr-4">{{ item.expected }}</td>
            <td class="pr-4">{{ item.answer ?? '-' }}</td>
            <td class="pr-4">{{ item.correct ? 'yes' : 'no' }}</td>
            <td class="pr-4">{{ item.misses.join(', ') }}</td>
            <td class="pr-4">{{ formatMicroUsd(item.costMicroUsd) }}</td>
            <td class="pr-4">{{ formatLatency(item.latencyMs) }}</td>
            <td class="pr-4">
              <Badge :variant="outcomeVariant(item.outcome)">{{ item.outcome }}</Badge>
            </td>
            <td>{{ item.rating ?? '-' }}</td>
          </tr>
        </tbody>
      </table>
    </Card>
  </div>
</template>
