import { flushPromises, mount } from '@vue/test-utils';
import { RouterHistoryMode } from '@wow-two-beta/fe/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import AppRoot from '../src/bootstrap/AppRoot.vue';
import { createWebRouter } from '../src/bootstrap/router/Routes';

const wrappers: ReturnType<typeof mount>[] = [];

afterEach(() => {
  for (const wrapper of wrappers.splice(0)) wrapper.unmount();
  vi.unstubAllGlobals();
});

const status = { data: { service: 'ai', status: 'ok', version: '0.1.4+abc1234' } };
const greeting = { data: { message: 'Hello, world!', greetedAtUtc: '2026-10-26T00:00:00+00:00' } };

/** Answers the console's two same-origin calls: the service status and the greetings contract. */
function answer(input: RequestInfo | URL) {
  const url = new URL(input instanceof Request ? input.url : String(input), 'https://ai.example.test');
  return Response.json(url.pathname === '/api/system/status' ? status : greeting);
}

/** Mounts the app at `path`, routed as in the browser but over memory history. */
async function renderAt(path: string) {
  const router = createWebRouter(RouterHistoryMode.Memory);
  await router.push(path);
  await router.isReady();
  const wrapper = mount(AppRoot, { global: { plugins: [router] } });
  wrappers.push(wrapper);
  await flushPromises();
  return wrapper;
}

describe('console route', () => {
  it('shows the running versions, composes the admin status panel and embeds the greeting widget', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL) => answer(input)),
    );
    const wrapper = await renderAt('/');

    expect(wrapper.get('[aria-label="App version"]').text()).toBe('app test');
    expect(wrapper.get('[aria-label="API version"]').text()).toBe('api 0.1.4+abc1234');
    expect(wrapper.get('[aria-label="Greetings contract status"]').text()).toBe('answering');
    expect(wrapper.find('input[name="name"]').exists()).toBe(true);
  });
});

describe('hosted greeting route', () => {
  it('starts with the forwarded name and links back to the forwarding app', async () => {
    const wrapper = await renderAt('/greet?name=Ann&return_url=https%3A%2F%2Fapp.test%2Fdone');

    expect((wrapper.get('input[name="name"]').element as HTMLInputElement).value).toBe('Ann');
    expect(wrapper.get('a').attributes('href')).toBe('https://app.test/done');
  });

  it('offers no way back when the forwarding app gave no usable return URL', async () => {
    const wrapper = await renderAt('/greet?return_url=javascript%3Aalert(1)');

    expect(wrapper.find('input[name="name"]').exists()).toBe(true);
    expect(wrapper.find('a').exists()).toBe(false);
  });
});
