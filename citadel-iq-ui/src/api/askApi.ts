import { postSse } from './sse';
import type { AnswerSourceDto, AnswerUsageDto, AskRequestDto } from '../types/ask';

export interface AskHandlers {
  onQuestion: (standaloneQuestion: string, rewritten: boolean) => void;
  onSources: (sources: AnswerSourceDto[]) => void;
  onText: (delta: string) => void;
  onNotFound: () => void;
  onDone: (done: { citedSources: number[]; verified: boolean; usage?: AnswerUsageDto }) => void;
  onError: (message: string) => void;
}

export function ask(request: AskRequestDto, handlers: AskHandlers, signal: AbortSignal): Promise<void> {
  return postSse(
    '/api/answers/stream',
    request,
    ({ event, data }) => {
      const payload = JSON.parse(data);
      switch (event) {
        case 'question':
          handlers.onQuestion(payload.standaloneQuestion, payload.rewritten);
          break;
        case 'sources':
          handlers.onSources(payload.sources);
          break;
        case 'text':
          handlers.onText(payload.delta);
          break;
        case 'notfound':
          handlers.onNotFound();
          break;
        case 'done':
          handlers.onDone(payload);
          break;
        case 'error':
          handlers.onError(payload.message);
          break;
      }
    },
    signal,
  );
}

export const askApi = { ask };
