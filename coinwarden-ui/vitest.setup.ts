import '@testing-library/jest-dom';

// Polyfill crypto.randomUUID for jsdom environments that don't provide it.
if (!globalThis.crypto) {
  (globalThis as unknown as { crypto: Partial<Crypto> }).crypto = {};
}

if (!globalThis.crypto.randomUUID) {
  (globalThis.crypto as unknown as { randomUUID: () => string }).randomUUID = () =>
    '00000000-0000-0000-0000-000000000000';
}

// Required for React 18+ act() in test environments.
(globalThis as unknown as { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
