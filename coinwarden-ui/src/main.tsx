import React, { lazy, Suspense } from 'react';
import ReactDOM from 'react-dom/client';
import { HashRouter as Router } from 'react-router';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import App from './App.tsx';
import { ErrorBoundary } from './components/ErrorBoundary';
import './index.css';

// Styling via Tailwind v4 + shadcn/ui tokens defined in index.css; no UI provider needed.

// Dev-only React Query Devtools: lazily imported behind import.meta.env.DEV so it is
// dead-code-eliminated from production bundles.
const DevTools = import.meta.env.DEV
  ? lazy(() => import('./components/DevTools').then(m => ({ default: m.DevTools })))
  : () => null;

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: (failureCount, error) => {
        if (error && typeof error === 'object' && 'status' in error && error.status === 404) {
          return false;
        }
        return failureCount < 2;
      },
      refetchOnWindowFocus: false,
      staleTime: 60 * 1000
    },
    mutations: {
      retry: false
    }
  }
});

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <Router>
        <ErrorBoundary>
          <App />
        </ErrorBoundary>
        <Suspense>
          <DevTools />
        </Suspense>
      </Router>
    </QueryClientProvider>
  </React.StrictMode>
);
