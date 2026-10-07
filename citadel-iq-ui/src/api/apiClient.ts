/**
 * Same origin: the UI reaches the API through the Vite dev proxy (dev) or nginx (Docker), so paths are relative and
 * the browser sends the HttpOnly session cookie automatically. No token is ever readable here.
 */
export const API_BASE_URL = '';

/** Every state-changing request carries this header; a cross-origin page cannot add it (CSRF defence). */
export const CSRF_HEADERS: Record<string, string> = { 'X-CSRF': '1' };

export class ApiError extends Error {
  status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

type AuthListener = (status: 401 | 403, title: string | undefined) => void;
let authListener: AuthListener | null = null;

/** The session provider registers here to react to "signed out" (401) and "account closed" (403) answers from any call. */
export function onAuthFailure(listener: AuthListener | null) {
  authListener = listener;
}

/** Called by every transport (fetch, SSE, XHR) with a failed response's status and ProblemDetails title. */
export function reportAuthFailure(status: number, title: string | undefined) {
  if (status === 401 || status === 403) {
    authListener?.(status, title);
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const method = init?.method ?? 'GET';
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    credentials: 'same-origin',
    headers: {
      'Content-Type': 'application/json',
      ...(method === 'GET' ? {} : CSRF_HEADERS),
      ...init?.headers,
    },
  });

  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    reportAuthFailure(response.status, problem?.title);
    throw new ApiError(problem?.title ?? 'Something went wrong. Please try again.', response.status);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export const apiClient = {
  get: <T>(path: string) => request<T>(path, { method: 'GET' }),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: body ? JSON.stringify(body) : undefined }),
  put: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'PUT', body: body ? JSON.stringify(body) : undefined }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
};
