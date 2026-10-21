import type { EmailDelivery, EmailOutcome, EmailReason } from '../../types/email';

type ChipColor = 'default' | 'success' | 'warning' | 'error' | 'info';

export const OUTCOME_LABELS: Record<EmailOutcome, { label: string; color: ChipColor }> = {
  AutoReplied: { label: 'Answered', color: 'success' },
  Drafted: { label: 'Drafted', color: 'warning' },
  NoConfidentAnswer: { label: 'No confident answer', color: 'info' },
  Ignored: { label: 'Ignored', color: 'default' },
  Paused: { label: 'Paused', color: 'default' },
  Failed: { label: 'Failed', color: 'error' },
};

export const REASON_LABELS: Record<EmailReason, string> = {
  NotAQuestion: 'Not a question',
  Thanks: 'Thank-you message',
  AutomatedMessage: 'Automated message',
  NotificationReply: 'Reply to a notification',
  OverLimit: 'Over limit',
  ThreadLimit: 'Conversation limit reached',
  AdminInThread: "Follow-up to an admin's reply",
  NoAnswer: 'No answer found',
  LowConfidence: 'Low confidence',
  DraftsOnly: 'Automatic replies paused',
  AnswersDisabled: 'Answers turned off',
  Failed: 'Processing failed',
};

export const DELIVERY_LABELS: Record<EmailDelivery, string> = {
  Pending: 'Sending',
  Sent: 'Sent',
  Failed: 'Reply could not be delivered',
  Cancelled: 'Not sent',
};

export function documentLink(folderId: string, documentId: string) {
  return `/folders/${folderId}?document=${documentId}`;
}

export function formatDateTime(iso: string) {
  return new Date(iso).toLocaleString(undefined, { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });
}

export function formatAge(iso: string) {
  const minutes = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 60000));
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 48) return `${hours} h ago`;
  return `${Math.round(hours / 24)} days ago`;
}
