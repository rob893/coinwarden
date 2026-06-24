import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, waitFor } from '@testing-library/react';
import { OAuthCallbackPage } from '../OAuthCallbackPage';
import { useAuth } from '../../hooks/useAuth';

vi.mock('../../hooks/useAuth', () => ({
  useAuth: vi.fn()
}));

const navigateMock = vi.fn();
vi.mock('react-router', () => ({
  useNavigate: () => navigateMock
}));

const GITHUB_STATE_KEY = 'github_oauth_state';

/** Builds a complete auth context value, overriding only the fields under test. */
function makeAuth(overrides: Partial<ReturnType<typeof useAuth>>): ReturnType<typeof useAuth> {
  return {
    user: null,
    isAuthenticated: false,
    isLoading: false,
    login: vi.fn(() => Promise.resolve()),
    loginWithGitHub: vi.fn(() => Promise.resolve()),
    loginWithGoogle: vi.fn(() => Promise.resolve()),
    register: vi.fn(() => Promise.resolve()),
    logout: vi.fn(() => Promise.resolve()),
    checkAuth: vi.fn(() => Promise.resolve()),
    ...overrides
  };
}

describe('OAuthCallbackPage', () => {
  beforeEach(() => {
    sessionStorage.clear();
    navigateMock.mockReset();
    vi.mocked(useAuth).mockReset();
  });

  afterEach(() => {
    vi.clearAllMocks();
    sessionStorage.clear();
    window.location.hash = '';
  });

  it('scrubs the OAuth code/state query from the URL hash after processing', async () => {
    const loginWithGitHub = vi.fn(() => Promise.resolve());
    vi.mocked(useAuth).mockReturnValue(makeAuth({ loginWithGitHub }));

    sessionStorage.setItem(GITHUB_STATE_KEY, 'state-xyz');
    window.location.hash = '#/auth/github/callback?code=abc123&state=state-xyz';

    render(<OAuthCallbackPage provider="github" />);

    await waitFor(() => {
      expect(loginWithGitHub).toHaveBeenCalledWith('abc123');
    });

    expect(window.location.hash).toBe('#/auth/github/callback');
    expect(window.location.hash).not.toContain('code=');
    expect(window.location.hash).not.toContain('state=');
  });

  it('scrubs the query even when state verification fails', async () => {
    vi.mocked(useAuth).mockReturnValue(makeAuth({}));

    sessionStorage.setItem(GITHUB_STATE_KEY, 'expected');
    window.location.hash = '#/auth/github/callback?code=abc123&state=tampered';

    render(<OAuthCallbackPage provider="github" />);

    await waitFor(() => {
      expect(window.location.hash).toBe('#/auth/github/callback');
    });
    expect(window.location.hash).not.toContain('code=');
    expect(window.location.hash).not.toContain('state=');
  });
});
