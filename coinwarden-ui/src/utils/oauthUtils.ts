/**
 * Generic OAuth utility functions for multiple providers.
 *
 * The authorization flow is initiated server-side: the SPA navigates to the API's
 * `/{provider}/start` endpoint, which generates and binds the anti-CSRF `state` and the PKCE
 * `code_verifier` to an HttpOnly cookie before redirecting to the provider. The SPA therefore
 * no longer builds the authorize URL, holds the client IDs, or manages `state` itself — the
 * server is the authority for CSRF/PKCE binding.
 */

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

export type OAuthProvider = 'github' | 'google';

export interface OAuthCallbackResult {
  code?: string;
  error?: string;
  errorDescription?: string;
}

/** API endpoints that begin the server-driven OAuth authorization flow. */
const OAUTH_START_PATHS: Record<OAuthProvider, string> = {
  github: '/api/v1/auth/github/start',
  google: '/api/v1/auth/google/start'
};

/**
 * Begins an OAuth login by navigating to the API's server-side start endpoint, which binds the
 * `state`/PKCE verifier to a cookie and redirects on to the provider.
 *
 * @param provider The OAuth provider to authenticate with.
 */
export function redirectToOAuth(provider: OAuthProvider): void {
  window.location.href = `${API_BASE_URL}${OAUTH_START_PATHS[provider]}`;
}

/**
 * Reads the OAuth callback result from the current URL hash (the app uses HashRouter).
 *
 * @returns The parsed callback result, or null when the hash carries no query string.
 */
export function handleOAuthCallbackFromUrl(): OAuthCallbackResult | null {
  const hash = window.location.hash;

  const queryStringIndex = hash.indexOf('?');
  if (queryStringIndex === -1) {
    return null;
  }

  const queryString = hash.substring(queryStringIndex + 1);
  const searchParams = new URLSearchParams(queryString);
  return handleOAuthCallback(searchParams);
}

/**
 * Parses an OAuth callback's query parameters into a result. The anti-CSRF `state` is validated
 * server-side (cookie-bound) before the API redirects here, so the SPA only needs the `code`.
 *
 * @param searchParams The callback query parameters.
 * @returns The parsed callback result.
 */
export function handleOAuthCallback(searchParams: URLSearchParams): OAuthCallbackResult | null {
  const code = searchParams.get('code');
  const error = searchParams.get('error');
  const errorDescription = searchParams.get('error_description');

  if (error) {
    return {
      error,
      errorDescription: errorDescription || undefined
    };
  }

  if (code) {
    return { code };
  }

  return {
    error: 'missing_code',
    errorDescription: 'Authorization code not found in callback.'
  };
}

export const redirectToGitHubOAuth = () => redirectToOAuth('github');
export const redirectToGoogleOAuth = () => redirectToOAuth('google');
