import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { GreetingsStatusPanel } from '../src/admin';
import { createGreetingsClient } from '../src/kit';

const serviceUrl = 'https://ai.example.test';
const greeting = { message: 'Hello, world!', greetedAtUtc: '2026-10-26T00:00:00+00:00' };
const wrappers: ReturnType<typeof mount>[] = [];

afterEach(() => {
  for (const wrapper of wrappers.splice(0)) wrapper.unmount();
});

async function render(...responses: (() => Response)[]) {
  const fetch = vi.fn<typeof globalThis.fetch>(async () => (responses.shift() ?? (() => Response.error()))());
  const wrapper = mount(GreetingsStatusPanel, {
    props: { client: createGreetingsClient({ baseUrl: serviceUrl, fetch }) },
  });
  wrappers.push(wrapper);
  await flushPromises();
  return { fetch, wrapper };
}

const status = (wrapper: ReturnType<typeof mount>) => wrapper.get('[aria-label="Greetings contract status"]').text();

describe('greetings status panel', () => {
  it('probes the contract through the client on mount and reports it answering', async () => {
    const { fetch, wrapper } = await render(() => Response.json({ data: greeting }));

    expect(status(wrapper)).toBe('answering');
    expect(wrapper.get('time').attributes('datetime')).toBe(greeting.greetedAtUtc);
    expect(new URL(String(fetch.mock.calls[0]?.[0])).pathname).toBe('/api/greetings');
  });

  it('reports a failing contract as not answering, with the failure', async () => {
    const { wrapper } = await render(() => Response.json({ title: 'Unavailable' }, { status: 503 }));

    expect(status(wrapper)).toBe('not answering');
    expect(wrapper.find('[role="alert"]').exists()).toBe(true);
    expect(wrapper.find('time').exists()).toBe(false);
  });

  it('probes again on request', async () => {
    const { fetch, wrapper } = await render(
      () => Response.json({ title: 'Unavailable' }, { status: 503 }),
      () => Response.json({ data: greeting }),
    );

    await wrapper.get('button').trigger('click');
    await flushPromises();

    expect(fetch).toHaveBeenCalledTimes(2);
    expect(status(wrapper)).toBe('answering');
    expect(wrapper.find('[role="alert"]').exists()).toBe(false);
  });
});
