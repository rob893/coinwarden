/**
 * Content-Security-Policy helpers for the production SPA build.
 *
 * The frontend (SPA) and backend (API) are deployed on different origins, so `connect-src`
 * must explicitly allow the API origin (derived from `VITE_API_BASE_URL`) in addition to
 * `'self'`; otherwise every authenticated XHR to the API would be blocked.
 */

/**
 * Builds the baseline Content-Security-Policy string shipped with production builds.
 *
 * @param apiBaseUrl The configured API base URL (`VITE_API_BASE_URL`). When it points at a
 * different origin, that origin is added to `connect-src`. A missing/relative value implies a
 * same-origin API, which `'self'` already covers.
 * @returns A `;`-separated CSP directive string suitable for a `<meta http-equiv>` tag.
 */
export function buildContentSecurityPolicy(apiBaseUrl: string | undefined): string {
  const connectSrc = ["'self'"];
  const apiOrigin = toOrigin(apiBaseUrl);

  if (apiOrigin) {
    connectSrc.push(apiOrigin);
  }

  const directives = [
    "default-src 'self'",
    // Keep script-src free of 'unsafe-inline'/'unsafe-eval' so injected scripts cannot execute.
    "script-src 'self'",
    // Tailwind injects inline styles, so 'unsafe-inline' is required for style-src.
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data:",
    "font-src 'self' data:",
    `connect-src ${connectSrc.join(' ')}`,
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    // frame-ancestors is ignored when CSP is delivered via <meta>; enforce it (and
    // X-Content-Type-Options/Referrer-Policy/HSTS) at the hosting layer for clickjacking defense.
    "frame-ancestors 'none'"
  ];

  return directives.join('; ');
}

/**
 * Extracts the origin (scheme + host + port) from a URL, returning null for missing or
 * relative/malformed values (which represent a same-origin API).
 */
function toOrigin(url: string | undefined): string | null {
  if (!url) {
    return null;
  }

  try {
    return new URL(url).origin;
  } catch {
    return null;
  }
}
