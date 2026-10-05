import type { SearchResultDto, SearchScope } from './search';

export interface AppSettings {
  askEnabled: boolean;
  maxQuestionLength: number;
  maxHistoryTurns: number;
  maxHistoryChars: number;
}

export interface ConversationTurnDto {
  question: string;
  answer: string;
}

export interface AskRequestDto {
  question: string;
  currentFolderId: string;
  searchScope: SearchScope;
  history: ConversationTurnDto[];
}

/** A search result plus the citation number the model refers to it by. */
export interface AnswerSourceDto extends SearchResultDto {
  number: number;
}

export interface AnswerUsageDto {
  inputTokens: number;
  outputTokens: number;
}

export type TurnStatus = 'streaming' | 'done' | 'notfound' | 'error';

export interface ConversationTurn {
  id: number;
  question: string;
  standaloneQuestion?: string;
  rewritten?: boolean;
  answer: string;
  sources: AnswerSourceDto[];
  cited: number[];
  verified?: boolean;
  status: TurnStatus;
  error?: string;
  /** True when the failure was a 429 / provider error, so the UI can phrase it accordingly. */
  searchInstead?: boolean;
}
