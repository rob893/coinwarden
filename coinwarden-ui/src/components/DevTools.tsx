import { ReactQueryDevtools } from '@tanstack/react-query-devtools';

/**
 * Wraps the React Query Devtools so it can be lazily imported and rendered only
 * in development builds, keeping it out of the production bundle.
 */
export function DevTools() {
  return <ReactQueryDevtools initialIsOpen={false} />;
}
