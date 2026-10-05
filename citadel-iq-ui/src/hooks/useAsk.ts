import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { askApi } from '../api/askApi';
import { ApiError } from '../api/apiClient';
import { useAppSettings } from '../settings/useAppSettings';
import { stripCitationMarkers } from '../utils/citations';
import type { ConversationTurn, ConversationTurnDto } from '../types/ask';
import type { SearchScope } from '../types/search';

export const NOT_FOUND_MESSAGE = "I couldn't find an answer to this in your documents.";

const GENERIC_ERROR = 'Something went wrong while answering. Please try again.';

/** Every turn that produced an answer (including "not found") becomes history; failed/aborted turns have nothing to record. */
export function buildHistory(turns: ConversationTurn[]): ConversationTurnDto[] {
  return turns.flatMap((turn): ConversationTurnDto[] => {
    if (turn.status === 'done') return [{ question: turn.question, answer: stripCitationMarkers(turn.answer) }];
    if (turn.status === 'notfound') return [{ question: turn.question, answer: NOT_FOUND_MESSAGE }];
    return [];
  });
}

export function useAsk(currentFolderId: string) {
  const settings = useAppSettings();
  const [input, setInput] = useState('');
  const [scope, setScope] = useState<SearchScope>('CurrentFolder');
  const [turns, setTurns] = useState<ConversationTurn[]>([]);
  const [inputError, setInputError] = useState<string | null>(null);
  const controllerRef = useRef<AbortController | null>(null);
  const nextIdRef = useRef(1);

  const isStreaming = turns.some((t) => t.status === 'streaming');

  const limitReached = useMemo(() => {
    if (isStreaming) return false;
    const history = buildHistory(turns);
    const chars = history.reduce((sum, t) => sum + t.question.length + t.answer.length, 0);
    return history.length >= settings.maxHistoryTurns || chars >= settings.maxHistoryChars;
  }, [turns, isStreaming, settings.maxHistoryTurns, settings.maxHistoryChars]);

  const updateTurn = useCallback((id: number, patch: (turn: ConversationTurn) => Partial<ConversationTurn>) => {
    setTurns((all) => all.map((t) => (t.id === id ? { ...t, ...patch(t) } : t)));
  }, []);

  const abort = useCallback(() => {
    controllerRef.current?.abort();
    controllerRef.current = null;
  }, []);

  useEffect(() => abort, [abort]);

  const submit = useCallback(async () => {
    const question = input.trim();
    if (!question || isStreaming) return;
    if (question.length > settings.maxQuestionLength) {
      setInputError(`Keep your question under ${settings.maxQuestionLength} characters.`);
      return;
    }

    setInputError(null);
    const history = buildHistory(turns);
    const id = nextIdRef.current++;
    const controller = new AbortController();
    controllerRef.current = controller;

    setTurns((all) => [...all, { id, question, answer: '', sources: [], cited: [], status: 'streaming' }]);
    setInput('');

    let terminal = false;
    try {
      await askApi.ask(
        { question, currentFolderId, searchScope: scope, history },
        {
          onQuestion: (standaloneQuestion, rewritten) => updateTurn(id, () => ({ standaloneQuestion, rewritten })),
          onSources: (sources) => updateTurn(id, () => ({ sources })),
          onText: (delta) => updateTurn(id, (t) => ({ answer: t.answer + delta })),
          onNotFound: () => updateTurn(id, () => ({ status: 'notfound' })),
          onDone: ({ citedSources, verified }) => {
            terminal = true;
            updateTurn(id, (t) => ({
              cited: citedSources,
              verified,
              status: t.status === 'notfound' ? 'notfound' : 'done',
            }));
          },
          onError: (message) => {
            terminal = true;
            updateTurn(id, () => ({ status: 'error', error: message || GENERIC_ERROR, searchInstead: true }));
          },
        },
        controller.signal,
      );
      if (!terminal) {
        updateTurn(id, () => ({ status: 'error', error: GENERIC_ERROR, searchInstead: true }));
      }
    } catch (err) {
      if (controller.signal.aborted) {
        updateTurn(id, () => ({ status: 'error', error: 'Stopped before the answer finished.' }));
      } else {
        updateTurn(id, () => ({
          status: 'error',
          error: err instanceof ApiError ? err.message : GENERIC_ERROR,
          searchInstead: !(err instanceof ApiError) || err.status >= 500 || err.status === 429,
        }));
      }
    } finally {
      if (controllerRef.current === controller) controllerRef.current = null;
    }
  }, [input, isStreaming, turns, settings.maxQuestionLength, currentFolderId, scope, updateTurn]);

  /** Clears the conversation, aborting any in-flight request. */
  const newSession = useCallback(() => {
    abort();
    setTurns([]);
    setInput('');
    setInputError(null);
  }, [abort]);

  return { input, setInput, scope, setScope, turns, isStreaming, limitReached, inputError, submit, abort, newSession };
}
