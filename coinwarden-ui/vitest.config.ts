import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import { fileURLToPath } from 'node:url';

export default defineConfig({
  // Vite (rolldown) and the Vite version bundled by Vitest expose structurally
  // incompatible Plugin types, so the plugin must be cast.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  plugins: [react() as any],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    }
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./vitest.setup.ts'],
    include: ['src/**/*.{test,spec}.{ts,tsx}']
  }
});
