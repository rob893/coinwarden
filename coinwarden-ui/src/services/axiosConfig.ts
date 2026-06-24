import axios, { AxiosError, type AxiosRequestConfig } from 'axios';
import { getDeviceId, getAccessToken, setAccessToken, clearAccessToken } from './auth';
import { ApiError, type ProblemDetailsError } from '../types/errors';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

const sessionId = crypto.randomUUID();

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json'
  }
});

/**
 * Reads the `csrf_token` value from `document.cookie`.
 *
 * The match is anchored to a cookie boundary (`^` or `; `) so a decoy cookie such
 * as `xcsrf_token` cannot be mistaken for it.
 *
 * @returns The decoded CSRF token, or `null` when the cookie is absent.
 */
export function getCsrfTokenFromCookie(): string | null {
  const match = document.cookie.match(/(?:^|;\s*)csrf_token=([^;]+)/);
  return match ? decodeURIComponent(match[1]) : null;
}

apiClient.interceptors.request.use(
  config => {
    const token = getAccessToken();

    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }

    const csrfToken = getCsrfTokenFromCookie();

    if (csrfToken) {
      config.headers['X-CSRF-Token'] = csrfToken;
    }

    config.headers['X-Correlation-Id'] = `${sessionId}:${crypto.randomUUID()}`;

    return config;
  },
  error => {
    return Promise.reject(error);
  }
);

let isRefreshing = false;
let failedQueue: Array<{
  resolve: (token: string | null) => void;
  reject: (err: unknown) => void;
}> = [];

const processQueue = (error: unknown, token: string | null = null) => {
  failedQueue.forEach(({ resolve, reject }) => {
    if (error) {
      reject(error);
    } else {
      resolve(token);
    }
  });

  failedQueue = [];
};

apiClient.interceptors.response.use(
  response => {
    return response;
  },
  async (error: AxiosError) => {
    const originalRequest = error.config as AxiosRequestConfig & { _retry?: boolean };

    if (error.response) {
      let problemDetails: ProblemDetailsError | undefined;

      try {
        if (error.response.data && typeof error.response.data === 'object') {
          if ('title' in error.response.data && 'status' in error.response.data) {
            problemDetails = error.response.data as ProblemDetailsError;
          }
        }
      } catch {
        // If parsing fails, use the default error message
      }

      const errorMessage =
        problemDetails?.detail ||
        problemDetails?.title ||
        `HTTP ${error.response.status}: ${error.response.statusText}`;

      const apiError = new ApiError(errorMessage, error.response.status, error.response.statusText, problemDetails);

      if (
        error.response.status === 401 &&
        (error.response.headers?.['x-token-expired'] || error.response.headers?.['X-Token-Expired']) &&
        !originalRequest._retry
      ) {
        if (isRefreshing) {
          return new Promise<string | null>((resolve, reject) => {
            failedQueue.push({ resolve, reject });
          })
            .then(token => {
              if (originalRequest.headers) {
                originalRequest.headers.Authorization = `Bearer ${token}`;
              }
              return apiClient(originalRequest);
            })
            .catch(err => {
              return Promise.reject(err);
            });
        }

        originalRequest._retry = true;
        isRefreshing = true;

        try {
          const refreshed = await refreshAccessToken();

          if (refreshed) {
            const newToken = getAccessToken();
            processQueue(null, newToken);

            if (originalRequest.headers) {
              originalRequest.headers.Authorization = `Bearer ${newToken}`;
            }

            return apiClient(originalRequest);
          } else {
            processQueue(apiError, null);
            clearAccessToken();

            return Promise.reject(apiError);
          }
        } catch (refreshError) {
          processQueue(refreshError, null);
          clearAccessToken();

          return Promise.reject(refreshError);
        } finally {
          isRefreshing = false;
        }
      }

      return Promise.reject(apiError);
    }

    const networkError = new ApiError(error.message || 'Network error', 0, 'Network Error');

    return Promise.reject(networkError);
  }
);

/**
 * Single source of truth for refreshing the access token.
 *
 * Uses a bare `axios` call (NOT `apiClient`) so it can be invoked from the
 * response interceptor without re-entering that interceptor and risking an
 * infinite 401 → refresh loop. Reads the CSRF token from the cookie, sends the
 * device id, persists the new access token, and reports success as a boolean.
 *
 * @returns `true` if a new access token was obtained and stored, otherwise `false`.
 */
export async function refreshAccessToken(): Promise<boolean> {
  try {
    const deviceId = getDeviceId();
    const csrfToken = getCsrfTokenFromCookie();

    if (!csrfToken) {
      console.error('CSRF token not found in cookie');
      return false;
    }

    const response = await axios.post(
      `${API_BASE_URL}/api/v1/auth/refreshToken`,
      { deviceId },
      {
        headers: {
          'Content-Type': 'application/json',
          'X-CSRF-Token': csrfToken
        },
        withCredentials: true
      }
    );

    if (response.status === 200 && response.data.token) {
      setAccessToken(response.data.token);
      return true;
    }
  } catch (error) {
    console.error('Token refresh failed:', error);
  }

  return false;
}

export default apiClient;
