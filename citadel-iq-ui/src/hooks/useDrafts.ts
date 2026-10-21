import { useCallback, useEffect, useState } from 'react';
import { emailApi } from '../api/emailApi';
import { ApiError } from '../api/apiClient';
import type { EmailDraftDto, EmailDraftSummaryDto } from '../types/email';

/** The pending review queue (Owner/Admin). Send/dismiss throw an Error with the server's safe message and always
 * refetch the queue afterwards (another admin may have handled the draft). */
export function useDrafts() {
  const [drafts, setDrafts] = useState<EmailDraftSummaryDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const refetch = useCallback(async () => {
    try {
      setDrafts(await emailApi.pendingDrafts());
      setError(null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Unable to load drafts right now.');
    }
  }, []);

  useEffect(() => {
    refetch();
  }, [refetch]);

  const send = async (id: string, text: string) => {
    try {
      await emailApi.sendDraft(id, text);
    } catch (err) {
      throw new Error(err instanceof ApiError ? err.message : 'Unable to send this reply right now.');
    } finally {
      await refetch();
    }
  };

  const dismiss = async (id: string) => {
    try {
      await emailApi.dismissDraft(id);
    } catch (err) {
      throw new Error(err instanceof ApiError ? err.message : 'Unable to dismiss this draft right now.');
    } finally {
      await refetch();
    }
  };

  return { drafts, error, refetch, send, dismiss };
}

/** One draft's details. Callers key the component by id, so a new selection starts from empty state; an answer
 * arriving after unmount is ignored. */
export function useDraft(id: string) {
  const [draft, setDraft] = useState<EmailDraftDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    emailApi
      .draft(id)
      .then((d) => {
        if (!cancelled) setDraft(d);
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof ApiError ? err.message : 'Unable to load this draft right now.');
      });
    return () => {
      cancelled = true;
    };
  }, [id]);

  return { draft, error };
}
