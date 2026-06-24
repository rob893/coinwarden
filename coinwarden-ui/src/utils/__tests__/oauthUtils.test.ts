import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { handleOAuthCallback, handleOAuthCallbackFromUrl } from '../oauthUtils';

describe('handleOAuthCallback', () => {
  it('returns the code when present (state is validated server-side)', () => {
    const params = new URLSearchParams({ code: 'auth-code' });

    const result = handleOAuthCallback(params);

    expect(result).toEqual({ code: 'auth-code' });
  });

  it('ignores any state in the query and still returns the code', () => {
    const params = new URLSearchParams({ code: 'auth-code', state: 'whatever' });

    const result = handleOAuthCallback(params);

    expect(result).toEqual({ code: 'auth-code' });
  });

  it('returns missing_code when no code is present', () => {
    const params = new URLSearchParams();

    const result = handleOAuthCallback(params);

    expect(result?.error).toBe('missing_code');
  });

  it('surfaces a provider error and description', () => {
    const params = new URLSearchParams({ error: 'access_denied', error_description: 'User denied' });

    const result = handleOAuthCallback(params);

    expect(result).toEqual({ error: 'access_denied', errorDescription: 'User denied' });
  });

  it('surfaces the server-side invalid_state error returned via the callback redirect', () => {
    const params = new URLSearchParams({ error: 'invalid_state' });

    const result = handleOAuthCallback(params);

    expect(result?.error).toBe('invalid_state');
  });
});

describe('handleOAuthCallbackFromUrl', () => {
  beforeEach(() => {
    window.location.hash = '';
  });

  afterEach(() => {
    window.location.hash = '';
  });

  it('returns null when the hash has no query string', () => {
    window.location.hash = '#/auth/github/callback';

    expect(handleOAuthCallbackFromUrl()).toBeNull();
  });

  it('parses the code from the hash query string', () => {
    window.location.hash = '#/auth/github/callback?code=abc123';

    const result = handleOAuthCallbackFromUrl();

    expect(result).toEqual({ code: 'abc123' });
  });

  it('surfaces an error carried in the hash query string', () => {
    window.location.hash = '#/auth/github/callback?error=invalid_state';

    const result = handleOAuthCallbackFromUrl();

    expect(result?.error).toBe('invalid_state');
  });
});
