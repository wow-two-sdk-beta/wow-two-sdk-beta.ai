<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Badge, BadgeVariant, Card } from '@wow-two-beta/fe/presentation/display';
import { computed, onMounted, onScopeDispose, shallowRef } from 'vue';
import type { GreetingModel } from '../contracts';
import { createGreetingsClient, type GreetingsClient } from '../kit/CreateGreetingsClient';

/**
 * Shows whether the greetings contract answers — a management panel for the service's console or any console that
 * imports `./admin`. It probes through the kit's client, so it reads the consumer's backend (Form 1) or the service.
 */
defineOptions({ name: 'GreetingsStatusPanel' });

const props = withDefaults(
  defineProps<{
    /** The contract endpoint to probe; the consumer's own origin when omitted. */
    client?: GreetingsClient;
  }>(),
  { client: undefined },
);

const client = props.client ?? createGreetingsClient();
const pending = shallowRef(true);
const greeting = shallowRef<GreetingModel | null>(null);
const failure = shallowRef<string | null>(null);
let controller: AbortController | null = null;

const state = computed(() => {
  if (pending.value) return { label: 'checking', variant: BadgeVariant.Neutral };
  return greeting.value
    ? { label: 'answering', variant: BadgeVariant.Success }
    : { label: 'not answering', variant: BadgeVariant.Danger };
});
const answeredAt = computed(() => greeting.value && new Date(greeting.value.greetedAtUtc).toLocaleString());

/** Probes the contract with a blank-name greeting, the one call every form answers. */
async function check() {
  controller?.abort();
  const current = new AbortController();
  controller = current;
  pending.value = true;

  const result = await client.greet('', current.signal);
  if (current.signal.aborted) return;
  pending.value = false;
  greeting.value = result.ok ? result.value : null;
  failure.value = result.ok ? null : result.failure.message;
}

onMounted(check);
onScopeDispose(() => controller?.abort());
</script>

<template>
  <Card class="p-6">
    <div class="flex flex-wrap items-center gap-3">
      <p class="font-medium">Greetings contract</p>
      <span role="status" aria-label="Greetings contract status">
        <Badge :variant="state.variant">{{ state.label }}</Badge>
      </span>
    </div>
    <p v-if="greeting" class="mt-3 text-sm text-muted-foreground">
      Last answer <time :datetime="greeting.greetedAtUtc">{{ answeredAt }}</time>
    </p>
    <p v-else-if="failure" class="mt-3 text-sm text-destructive" role="alert">{{ failure }}</p>
    <Button class="mt-4" variant="outline" :is-loading="pending" @click="check">Check again</Button>
  </Card>
</template>
