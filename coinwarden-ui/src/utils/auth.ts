import { jwtDecode } from 'jwt-decode';
import type { User } from '../types/auth';

interface JwtPayload {
  nameid: number;
  unique_name: string;
  email: string;
  email_verified: boolean;
  role?: string | string[];
  exp: number;
  iat: number;
  nbf: number;
  iss: string;
  aud: string;
}

export function decodeJwtToken(token: string): User | null {
  try {
    const decoded = jwtDecode<JwtPayload>(token);

    let roles: string[] = [];
    if (decoded.role) {
      roles = Array.isArray(decoded.role) ? decoded.role : [decoded.role];
    }

    return {
      id: decoded.nameid,
      userName: decoded.unique_name,
      email: decoded.email,
      roles
    };
  } catch (error) {
    console.error('Failed to decode JWT token:', error);
    return null;
  }
}

/**
 * Returns whether the given user has the `Admin` role.
 *
 * @param user The user to check (may be `null` when unauthenticated).
 * @returns `true` if the user is non-null and has the `Admin` role, otherwise `false`.
 */
export function hasAdminRole(user: User | null): boolean {
  return user?.roles?.includes('Admin') ?? false;
}

/**
 * Returns whether a JWT access token is expired based on its `exp` claim.
 *
 * Note: the API is the source of truth for expiry — it signals an expired token via
 * the `x-token-expired` response header, which the axios interceptor uses to refresh.
 * This helper is a convenience for client-side checks and treats undecodable tokens
 * as expired.
 *
 * @param token The encoded JWT access token.
 * @returns `true` if the token is expired or cannot be decoded, otherwise `false`.
 */
export function isTokenExpired(token: string): boolean {
  try {
    const decoded = jwtDecode<JwtPayload>(token);
    const currentTime = Date.now() / 1000;
    return decoded.exp < currentTime;
  } catch {
    return true;
  }
}
