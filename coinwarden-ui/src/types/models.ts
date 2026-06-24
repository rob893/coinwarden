// Generic pagination types

export interface PageInfo {
  hasNextPage: boolean;
  hasPreviousPage: boolean;
  startCursor?: string;
  endCursor?: string;
  totalCount?: number;
}

export interface Edge<T> {
  node: T;
  cursor: string;
}

export interface CursorPaginatedResponse<T> {
  nodes?: T[];
  edges?: Edge<T>[];
  pageInfo: PageInfo;
}

export interface CursorPaginationQueryParameters {
  first?: number;
  after?: string;
  last?: number;
  before?: string;
}

// Note types
export interface Note {
  id: number;
  title: string;
  content: string;
  createdAt: string;
  updatedAt: string;
  userId: number;
}

export interface CreateNoteRequest {
  title: string;
  content: string;
}

export interface UpdateNoteRequest {
  title: string;
  content: string;
}

// Hello types
export interface HelloResponse {
  message: string;
  version: string;
}
