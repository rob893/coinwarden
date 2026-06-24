import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { decodeJwtToken, hasAdminRole, isTokenExpired } from '../auth';
import type { User } from '../../types/auth';

/**
 * Encodes a JSON payload into a JWT-shaped string. jwt-decode only base64-decodes
 * the payload segment, so the header/signature can be arbitrary.
 */
function makeToken(payload: Record<string, unknown>): string {
  const encode = (value: object): string => Buffer.from(JSON.stringify(value)).toString('base64url');
  const header = encode({ alg: 'HS512', typ: 'JWT' });
  const body = encode(payload);
  return `${header}.${body}.signature`;
}

const basePayload = {
  nameid: 42,
  unique_name: 'jane.doe',
  email: 'jane@example.com',
  email_verified: true,
  exp: 9999999999,
  iat: 1000000000,
  nbf: 1000000000,
  iss: 'coinwarden',
  aud: 'coinwarden-ui'
};

describe('decodeJwtToken', () => {
  let consoleErrorSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => undefined);
  });

  afterEach(() => {
    consoleErrorSpy.mockRestore();
  });

  it('maps standard claims to a User object', () => {
    const user = decodeJwtToken(makeToken({ ...basePayload, role: 'Admin' }));
    expect(user).not.toBeNull();
    expect(user?.id).toBe(42);
    expect(user?.userName).toBe('jane.doe');
    expect(user?.email).toBe('jane@example.com');
  });

  it('normalizes a single string role into a one-element array', () => {
    const user = decodeJwtToken(makeToken({ ...basePayload, role: 'Admin' }));
    expect(user?.roles).toEqual(['Admin']);
  });

  it('preserves an array of roles', () => {
    const user = decodeJwtToken(makeToken({ ...basePayload, role: ['Admin', 'User'] }));
    expect(user?.roles).toEqual(['Admin', 'User']);
  });

  it('returns an empty roles array when the role claim is missing', () => {
    const user = decodeJwtToken(makeToken({ ...basePayload }));
    expect(user?.roles).toEqual([]);
  });

  it('returns an empty roles array when role is an empty array', () => {
    const user = decodeJwtToken(makeToken({ ...basePayload, role: [] }));
    expect(user?.roles).toEqual([]);
  });

  it('returns null for a malformed token', () => {
    expect(decodeJwtToken('not-a-real-token')).toBeNull();
  });

  it('returns null for garbage payload that is not valid base64 JSON', () => {
    expect(decodeJwtToken('header.@@@not-base64@@@.signature')).toBeNull();
  });

  it('returns null for an empty string', () => {
    expect(decodeJwtToken('')).toBeNull();
  });
});

describe('hasAdminRole', () => {
  const makeUser = (roles: string[]): User => ({ id: 1, userName: 'jane', email: 'jane@example.com', roles });

  it('returns true when the user has the Admin role', () => {
    expect(hasAdminRole(makeUser(['Admin', 'User']))).toBe(true);
  });

  it('returns false when the user lacks the Admin role', () => {
    expect(hasAdminRole(makeUser(['User']))).toBe(false);
  });

  it('returns false when the user has no roles', () => {
    expect(hasAdminRole(makeUser([]))).toBe(false);
  });

  it('returns false for a null user', () => {
    expect(hasAdminRole(null)).toBe(false);
  });
});

describe('isTokenExpired', () => {
  it('returns false for a token whose exp is in the future', () => {
    expect(isTokenExpired(makeToken({ ...basePayload, exp: 9999999999 }))).toBe(false);
  });

  it('returns true for a token whose exp is in the past', () => {
    expect(isTokenExpired(makeToken({ ...basePayload, exp: 1000 }))).toBe(true);
  });

  it('returns true for a malformed token', () => {
    expect(isTokenExpired('not-a-real-token')).toBe(true);
  });

  it('returns true for an empty string', () => {
    expect(isTokenExpired('')).toBe(true);
  });
});
