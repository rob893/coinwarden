import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { AuthProvider } from '../AuthContext';
import { useAuth } from '../../hooks/useAuth';
import { authApi, getAccessToken, clearAccessToken } from '../../services/auth';
import type { LoginResponse, User } from '../../types/auth';

vi.mock('../../services/auth', () => ({
  authApi: {
    login: vi.fn(),
    loginWithGitHub: vi.fn(),
    loginWithGoogle: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
    refreshToken: vi.fn()
  },
  getAccessToken: vi.fn(),
  clearAccessToken: vi.fn()
}));

/** Encodes a JWT-shaped token whose payload decodes to the given user claims. */
function makeToken(payload: Record<string, unknown>): string {
  const encode = (value: object): string => Buffer.from(JSON.stringify(value)).toString('base64url');
  return `${encode({ alg: 'HS512', typ: 'JWT' })}.${encode(payload)}.sig`;
}

const sampleUser: User = { id: 1, userName: 'alice', email: 'alice@example.com', roles: [] };

function loginResponse(user: User = sampleUser): LoginResponse {
  return { token: 'response-token', user };
}

describe('AuthProvider / useAuth', () => {
  beforeEach(() => {
    window.location.hash = '#/login';
    vi.mocked(getAccessToken).mockReturnValue(null);
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
  });

  afterEach(() => {
    vi.clearAllMocks();
    vi.restoreAllMocks();
    window.location.hash = '';
  });

  it('sets the user and authenticated flag on successful login', async () => {
    vi.mocked(authApi.login).mockResolvedValue(loginResponse());
    const { result } = renderHook(() => useAuth(), { wrapper: AuthProvider });

    await act(async () => {
      await result.current.login({ userName: 'alice', password: 'pw' });
    });

    expect(result.current.isAuthenticated).toBe(true);
    expect(result.current.user?.userName).toBe('alice');
  });

  it('prefers user claims decoded from the access token when present', async () => {
    const token = makeToken({ nameid: 9, unique_name: 'token-user', email: 't@example.com', role: 'Admin' });
    vi.mocked(authApi.login).mockResolvedValue(loginResponse());
    vi.mocked(getAccessToken).mockReturnValue(token);
    const { result } = renderHook(() => useAuth(), { wrapper: AuthProvider });

    await act(async () => {
      await result.current.login({ userName: 'alice', password: 'pw' });
    });

    expect(result.current.user?.userName).toBe('token-user');
    expect(result.current.user?.roles).toEqual(['Admin']);
  });

  it('surfaces the error and stays unauthenticated on failed login', async () => {
    vi.mocked(authApi.login).mockRejectedValue(new Error('invalid credentials'));
    const { result } = renderHook(() => useAuth(), { wrapper: AuthProvider });

    await act(async () => {
      await expect(result.current.login({ userName: 'alice', password: 'bad' })).rejects.toThrow(
        'invalid credentials'
      );
    });

    expect(result.current.isAuthenticated).toBe(false);
    expect(result.current.user).toBeNull();
  });

  it('clears state and the token on logout', async () => {
    vi.mocked(authApi.login).mockResolvedValue(loginResponse());
    vi.mocked(authApi.logout).mockResolvedValue(undefined);
    const { result } = renderHook(() => useAuth(), { wrapper: AuthProvider });

    await act(async () => {
      await result.current.login({ userName: 'alice', password: 'pw' });
    });
    expect(result.current.isAuthenticated).toBe(true);

    await act(async () => {
      await result.current.logout();
    });

    expect(clearAccessToken).toHaveBeenCalled();
    expect(result.current.isAuthenticated).toBe(false);
    expect(result.current.user).toBeNull();
  });

  it('skips the silent refresh bootstrap on public routes', async () => {
    window.location.hash = '#/login';
    const { result } = renderHook(() => useAuth(), { wrapper: AuthProvider });

    await act(async () => {
      await result.current.checkAuth();
    });

    expect(authApi.refreshToken).not.toHaveBeenCalled();
  });

  it('authenticates from an existing token on a protected route without refreshing', async () => {
    window.location.hash = '#/dashboard';
    const token = makeToken({ nameid: 7, unique_name: 'bob', email: 'bob@example.com', role: ['Admin', 'User'] });
    vi.mocked(getAccessToken).mockReturnValue(token);
    const { result } = renderHook(() => useAuth(), { wrapper: AuthProvider });

    await waitFor(() => expect(result.current.isAuthenticated).toBe(true));

    expect(authApi.refreshToken).not.toHaveBeenCalled();
    expect(result.current.user?.roles).toEqual(['Admin', 'User']);
  });

  it('runs the silent refresh on a protected route when no token is cached', async () => {
    window.location.hash = '#/dashboard';
    const token = makeToken({ nameid: 3, unique_name: 'carol', email: 'carol@example.com', role: 'User' });
    vi.mocked(getAccessToken).mockReturnValueOnce(null).mockReturnValue(token);
    vi.mocked(authApi.refreshToken).mockResolvedValue({ token });
    const { result } = renderHook(() => useAuth(), { wrapper: AuthProvider });

    await waitFor(() => expect(result.current.isAuthenticated).toBe(true));

    expect(authApi.refreshToken).toHaveBeenCalledTimes(1);
    expect(result.current.user?.userName).toBe('carol');
  });

  it('clears state when the silent refresh fails on a protected route', async () => {
    window.location.hash = '#/dashboard';
    vi.mocked(getAccessToken).mockReturnValue(null);
    vi.mocked(authApi.refreshToken).mockRejectedValue(new Error('no session'));
    const { result } = renderHook(() => useAuth(), { wrapper: AuthProvider });

    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.isAuthenticated).toBe(false);
    expect(clearAccessToken).toHaveBeenCalled();
  });
});
