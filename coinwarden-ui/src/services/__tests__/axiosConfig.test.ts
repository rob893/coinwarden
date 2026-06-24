import { describe, it, expect, vi, beforeEach, afterEach, beforeAll, afterAll, type MockInstance } from 'vitest';
import axios, {
  AxiosError,
  AxiosHeaders,
  type AxiosAdapter,
  type AxiosResponse,
  type InternalAxiosRequestConfig
} from 'axios';
import apiClient, { refreshAccessToken, getCsrfTokenFromCookie } from '../axiosConfig';
import { getAccessToken, setAccessToken } from '../auth';
import { ApiError } from '../../types/errors';

const CSRF_COOKIE = 'csrf_token=test-csrf-token';

function setCsrfCookie(): void {
  document.cookie = CSRF_COOKIE;
}

function clearCsrfCookie(): void {
  document.cookie = 'csrf_token=; max-age=0';
}

/** Builds a 200 OK axios response for the supplied request config. */
function okResponse(config: InternalAxiosRequestConfig): AxiosResponse {
  return {
    data: { ok: true },
    status: 200,
    statusText: 'OK',
    headers: {},
    config
  };
}

/** Builds a rejected 401 carrying the `x-token-expired` marker header. */
function unauthorized(config: InternalAxiosRequestConfig): AxiosError {
  const response: AxiosResponse = {
    data: { title: 'Unauthorized', status: 401 },
    status: 401,
    statusText: 'Unauthorized',
    headers: { 'x-token-expired': 'true' },
    config
  };
  return new AxiosError('Unauthorized', 'ERR_BAD_REQUEST', config, null, response);
}

describe('getCsrfTokenFromCookie', () => {
  function clearAllCookies(): void {
    for (const pair of document.cookie.split(';')) {
      const name = pair.split('=')[0].trim();
      if (name) {
        document.cookie = `${name}=; max-age=0`;
      }
    }
  }

  beforeEach(() => {
    clearAllCookies();
  });

  afterEach(() => {
    clearAllCookies();
  });

  it('returns the token when csrf_token is among multiple cookies', () => {
    document.cookie = 'theme=dark';
    document.cookie = 'csrf_token=mid-value';
    document.cookie = 'session=abc';

    expect(getCsrfTokenFromCookie()).toBe('mid-value');
  });

  it('returns the token when csrf_token is at the start of the cookie string', () => {
    document.cookie = 'csrf_token=first-value';
    document.cookie = 'theme=dark';

    expect(getCsrfTokenFromCookie()).toBe('first-value');
  });

  it('does not match a decoy cookie such as xcsrf_token', () => {
    document.cookie = 'xcsrf_token=decoy-value';

    expect(getCsrfTokenFromCookie()).toBeNull();
  });

  it('prefers the real csrf_token over a decoy cookie present alongside it', () => {
    document.cookie = 'xcsrf_token=decoy-value';
    document.cookie = 'csrf_token=real-value';

    expect(getCsrfTokenFromCookie()).toBe('real-value');
  });

  it('decodes a URL-encoded token value', () => {
    document.cookie = 'csrf_token=a%2Bb%3D';

    expect(getCsrfTokenFromCookie()).toBe('a+b=');
  });

  it('returns null when no csrf_token cookie is present', () => {
    document.cookie = 'theme=dark';

    expect(getCsrfTokenFromCookie()).toBeNull();
  });
});

describe('refreshAccessToken', () => {
  let postSpy: MockInstance<typeof axios.post>;
  let consoleErrorSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    postSpy = vi.spyOn(axios, 'post');
    consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    setCsrfCookie();
    setAccessToken(null);
  });

  afterEach(() => {
    postSpy.mockRestore();
    consoleErrorSpy.mockRestore();
    setAccessToken(null);
  });

  it('stores the new token and returns true on a 200 response', async () => {
    postSpy.mockResolvedValue({ status: 200, data: { token: 'fresh-token' } } as AxiosResponse);

    await expect(refreshAccessToken()).resolves.toBe(true);
    expect(getAccessToken()).toBe('fresh-token');
  });

  it('sends the CSRF token read from the cookie in the refresh request', async () => {
    postSpy.mockResolvedValue({ status: 200, data: { token: 'fresh-token' } } as AxiosResponse);

    await refreshAccessToken();

    const headers = postSpy.mock.calls[0][2]?.headers as Record<string, string> | undefined;
    expect(headers?.['X-CSRF-Token']).toBe('test-csrf-token');
  });

  it('returns false without calling the API when no CSRF cookie is present', async () => {
    clearCsrfCookie();

    await expect(refreshAccessToken()).resolves.toBe(false);
    expect(postSpy).not.toHaveBeenCalled();
  });

  it('returns false when the refresh request rejects', async () => {
    postSpy.mockRejectedValue(new Error('network down'));

    await expect(refreshAccessToken()).resolves.toBe(false);
    expect(getAccessToken()).toBeNull();
  });

  it('returns false when the response contains no token', async () => {
    postSpy.mockResolvedValue({ status: 200, data: {} } as AxiosResponse);

    await expect(refreshAccessToken()).resolves.toBe(false);
  });
});

describe('apiClient 401 -> refresh -> retry interceptor', () => {
  let originalAdapter: AxiosAdapter | undefined;
  let postSpy: MockInstance<typeof axios.post>;
  let consoleErrorSpy: ReturnType<typeof vi.spyOn>;

  type SeenRequest = { id: string; attempt: number; auth: string | undefined };
  let attempts: Map<string, number>;
  let seen: SeenRequest[];

  function mockAdapter(config: InternalAxiosRequestConfig): Promise<AxiosResponse> {
    const headers = config.headers as AxiosHeaders;
    const id = String(headers.get('x-test-id') ?? 'default');
    const attempt = (attempts.get(id) ?? 0) + 1;
    attempts.set(id, attempt);
    const auth = headers.get('Authorization');
    seen.push({ id, attempt, auth: auth ? String(auth) : undefined });

    if (attempt === 1) {
      return Promise.reject(unauthorized(config));
    }
    return Promise.resolve(okResponse(config));
  }

  beforeAll(() => {
    originalAdapter = apiClient.defaults.adapter as AxiosAdapter | undefined;
  });

  afterAll(() => {
    apiClient.defaults.adapter = originalAdapter;
  });

  beforeEach(() => {
    attempts = new Map();
    seen = [];
    apiClient.defaults.adapter = mockAdapter as AxiosAdapter;
    postSpy = vi.spyOn(axios, 'post').mockResolvedValue({ status: 200, data: { token: 'newToken' } } as AxiosResponse);
    consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    setCsrfCookie();
    setAccessToken('oldToken');
  });

  afterEach(() => {
    postSpy.mockRestore();
    consoleErrorSpy.mockRestore();
    setAccessToken(null);
  });

  it('refreshes once and retries the original request with the new token', async () => {
    const response = await apiClient.get('/data', { headers: { 'x-test-id': 'req-1' } });

    expect(response.status).toBe(200);
    expect(postSpy).toHaveBeenCalledTimes(1);
    expect(getAccessToken()).toBe('newToken');

    const retry = seen.find(entry => entry.id === 'req-1' && entry.attempt === 2);
    expect(retry?.auth).toBe('Bearer newToken');
  });

  it('queues concurrent 401s and replays them after a single refresh', async () => {
    const [first, second] = await Promise.all([
      apiClient.get('/a', { headers: { 'x-test-id': 'c-1' } }),
      apiClient.get('/b', { headers: { 'x-test-id': 'c-2' } })
    ]);

    expect(first.status).toBe(200);
    expect(second.status).toBe(200);
    expect(postSpy).toHaveBeenCalledTimes(1);

    expect(seen.filter(entry => entry.attempt === 2)).toHaveLength(2);
    expect(seen.find(entry => entry.id === 'c-1' && entry.attempt === 2)?.auth).toBe('Bearer newToken');
    expect(seen.find(entry => entry.id === 'c-2' && entry.attempt === 2)?.auth).toBe('Bearer newToken');
  });

  it('rejects with an ApiError and clears the token when the refresh fails', async () => {
    postSpy.mockRejectedValue(new Error('refresh boom'));

    await expect(apiClient.get('/x', { headers: { 'x-test-id': 'f-1' } })).rejects.toBeInstanceOf(ApiError);
    expect(getAccessToken()).toBeNull();
  });
});
