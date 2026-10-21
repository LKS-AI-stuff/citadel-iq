/** Hand-kept in sync with CitadelIQ.Application/Dtos/EmailDtos.cs. */

export type EmailAgentMode = 'Off' | 'DraftsOnly' | 'On';

export type EmailOutcome = 'Ignored' | 'AutoReplied' | 'Drafted' | 'NoConfidentAnswer' | 'Paused' | 'Failed';

export type EmailReason =
  | 'NotAQuestion'
  | 'Thanks'
  | 'AutomatedMessage'
  | 'NotificationReply'
  | 'OverLimit'
  | 'ThreadLimit'
  | 'AdminInThread'
  | 'NoAnswer'
  | 'LowConfidence'
  | 'DraftsOnly'
  | 'AnswersDisabled'
  | 'Failed';

export type EmailDraftStatus = 'Pending' | 'Sent' | 'Dismissed' | 'Expired';

export type EmailDelivery = 'Pending' | 'Sent' | 'Failed' | 'Cancelled';

export interface EmailStatusDto {
  /** Null while the agent is off. */
  supportAddress: string | null;
  mode: EmailAgentMode;
  yourAddress: string;
  canUse: boolean;
  reason: 'NoEmail' | 'SharedAddress' | null;
}

export interface EmailPersonDto {
  userId: string;
  displayName: string;
}

export interface EmailDocumentDto {
  documentId: string;
  folderId: string;
  fileName: string;
}

export interface EmailActivityDto {
  id: string;
  receivedAtUtc: string;
  sender: EmailPersonDto;
  subject: string;
  outcome: EmailOutcome;
  reason: EmailReason | null;
  confidence: number | null;
  documents: EmailDocumentDto[];
  delivery: EmailDelivery | null;
}

export interface EmailDraftSummaryDto {
  id: string;
  questionId: string;
  asker: EmailPersonDto;
  subject: string;
  reason: EmailReason;
  status: EmailDraftStatus;
  createdAtUtc: string;
}

export interface EmailDraftSourceDto {
  number: number;
  documentId: string;
  folderId: string;
  fileName: string;
  page: number | null;
  sheet: string | null;
  cited: boolean;
}

export interface EmailDraftDto {
  id: string;
  questionId: string;
  status: EmailDraftStatus;
  reason: EmailReason;
  asker: EmailPersonDto;
  subject: string;
  question: string | null;
  receivedAtUtc: string;
  suggestedText: string | null;
  missing: string | null;
  sentText: string | null;
  confidence: number | null;
  sources: EmailDraftSourceDto[];
  createdAtUtc: string;
  expiresAtUtc: string;
}
