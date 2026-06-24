/**
 * Whether internal error details (correlation id, trace id, request path) may be
 * shown to the user.
 *
 * True during local development (`npm run dev`) and whenever any build —
 * including a production build — is served from `localhost` / `127.0.0.1`, so the
 * owner can inspect them locally. False for real end users on a deployed domain.
 */
export const showErrorDetails =
  import.meta.env.DEV || ['localhost', '127.0.0.1'].includes(window.location.hostname);
