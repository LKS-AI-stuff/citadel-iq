import { API_BASE_URL, ApiError, CSRF_HEADERS, reportAuthFailure } from './apiClient';

export interface SseMessage {
  event: string;
  data: string;
}

/** Incremental Server-Sent Events parser: feed it text chunks (which may split anywhere); it emits whole messages. */
export function createSseParser(onMessage: (message: SseMessage) => void) {
  let buffer = '';

  return (chunk: string) => {
    buffer = (buffer + chunk).replace(/\r\n/g, '\n');

    let boundary = buffer.indexOf('\n\n');
    while (boundary !== -1) {
      const frame = buffer.slice(0, boundary);
      buffer = buffer.slice(boundary + 2);

      let event = 'message';
      const data: string[] = [];
      for (const line of frame.split('\n')) {
        if (line.startsWith('event:')) event = line.slice(6).trim();
        else if (line.startsWith('data:')) data.push(line.slice(5).replace(/^ /, ''));
      }
      if (data.length > 0) onMessage({ event, data: data.join('\n') });

      boundary = buffer.indexOf('\n\n');
    }
  };
}

/** POSTs JSON and streams the SSE response. Non-2xx responses (sent before streaming starts) throw an ApiError. */
export async function postSse(
  path: string,
  body: unknown,
  onMessage: (message: SseMessage) => void,
  signal: AbortSignal,
): Promise<void> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'POST',
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream', ...CSRF_HEADERS },
    body: JSON.stringify(body),
    signal,
  });

  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    reportAuthFailure(response.status, problem?.title);
    throw new ApiError(problem?.title ?? 'Something went wrong. Please try again.', response.status);
  }

  if (!response.body) {
    throw new ApiError('The server did not return a stream.', response.status);
  }

  const parse = createSseParser(onMessage);
  const reader = response.body.getReader();
  const decoder = new TextDecoder();

  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    parse(decoder.decode(value, { stream: true }));
  }
  parse(decoder.decode());
}
