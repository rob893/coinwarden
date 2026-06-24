/**
 * Centralized application route paths.
 *
 * Shared between the router (`App.tsx`) and auth bootstrap logic
 * (`AuthContext`) so route definitions stay in one place.
 */
export const routePaths = {
  home: '/',
  login: '/login',
  register: '/register',
  forgotPassword: '/forgot-password',
  resetPassword: '/reset-password',
  gitHubCallback: '/auth/github/callback',
  googleCallback: '/auth/google/callback'
} as const;

/**
 * Routes that are reachable without authentication. The silent auth refresh on
 * app load is skipped while the user is on one of these pages.
 */
export const publicRoutePaths: readonly string[] = [
  routePaths.login,
  routePaths.register,
  routePaths.forgotPassword,
  routePaths.resetPassword,
  routePaths.gitHubCallback,
  routePaths.googleCallback
];

/**
 * Tests whether a given pathname belongs to a public/auth route.
 *
 * @param pathname Route pathname without the leading `#` or any query string
 * (e.g. `/login`, `/auth/github/callback`).
 * @returns `true` when the pathname matches a public route exactly or as a
 * nested segment of one.
 */
export function isPublicRoute(pathname: string): boolean {
  return publicRoutePaths.some(path => pathname === path || pathname.startsWith(`${path}/`));
}
