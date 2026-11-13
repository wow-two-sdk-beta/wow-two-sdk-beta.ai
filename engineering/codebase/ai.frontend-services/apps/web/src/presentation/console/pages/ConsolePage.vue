<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Badge, Card } from '@wow-two-beta/fe/presentation/display';
import { GreetingsStatusPanel } from '@wow-two-beta/fe-ai/admin';
import { GreetingWidget, forwardToHostedGreeting } from '@wow-two-beta/fe-ai/kit';
import { useSystemStatus } from '../../../application/system/UseSystemStatus';

/**
 * Renders the console: the running versions, the fe-ai package's management screens (`./admin`) and both modes of
 * its kit, consumed through its exports.
 */
defineOptions({ name: 'ConsolePage' });

const appVersion = __APP_VERSION__;
const { status } = useSystemStatus();

function forward() {
  forwardToHostedGreeting({
    serviceUrl: window.location.origin,
    name: 'Ann',
    returnUrl: window.location.href,
  });
}
</script>

<template>
  <main class="mx-auto min-h-screen max-w-2xl px-6 py-16 font-sans text-foreground">
    <header class="flex flex-wrap items-center gap-3">
      <h1 class="text-3xl font-semibold">Ai console</h1>
      <div class="contents" aria-label="App version">
        <Badge>app {{ appVersion }}</Badge>
      </div>
      <div v-if="status" class="contents" aria-label="API version">
        <Badge>api {{ status.version }}</Badge>
      </div>
    </header>

    <section class="mt-8" aria-labelledby="admin-heading">
      <h2 id="admin-heading" class="mb-3 text-sm font-medium text-muted-foreground">Status</h2>
      <GreetingsStatusPanel />
    </section>

    <section class="mt-8" aria-labelledby="embed-heading">
      <h2 id="embed-heading" class="mb-3 text-sm font-medium text-muted-foreground">Embedded widget</h2>
      <GreetingWidget />
    </section>

    <section class="mt-8" aria-labelledby="forward-heading">
      <Card class="p-6">
        <h2 id="forward-heading" class="text-sm font-medium text-muted-foreground">Forward to the hosted page</h2>
        <Button class="mt-3" @click="forward">Open the hosted greeting</Button>
      </Card>
    </section>
  </main>
</template>
