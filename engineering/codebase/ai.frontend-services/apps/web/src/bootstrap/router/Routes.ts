import { createAppRouter, lazyRoute, RouterHistoryMode, type RouteConfig } from '@wow-two-beta/fe/router';

/** Holds the route table: the console at the root; a route per hosted page is added here when one exists. */
export const routes: RouteConfig = [
  {
    path: '/',
    id: 'console',
    handle: { title: 'Console' },
    lazy: lazyRoute(() => import('../../presentation/console/pages/ConsolePage.vue')),
  },
];

/** Creates the app's router over `history`: the browser's in the app, memory in tests. */
export function createWebRouter(history: RouterHistoryMode = RouterHistoryMode.Browser) {
  return createAppRouter(routes, { titleSuffix: 'Ai', routePersistence: false, history });
}
