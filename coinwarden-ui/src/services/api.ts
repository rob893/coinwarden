import apiClient from './axiosConfig';
import type {
  CursorPaginatedResponse,
  CursorPaginationQueryParameters,
  Note,
  CreateNoteRequest,
  UpdateNoteRequest,
  HelloResponse
} from '../types/models';

export const helloApi = {
  async getHelloV1(): Promise<HelloResponse> {
    const response = await apiClient.get<HelloResponse>('/api/v1/hello');
    return response.data;
  },

  async getHelloV2(): Promise<HelloResponse> {
    const response = await apiClient.get<HelloResponse>('/api/v2/hello');
    return response.data;
  }
};

export const notesApi = {
  async getNotes(params?: CursorPaginationQueryParameters): Promise<CursorPaginatedResponse<Note>> {
    const response = await apiClient.get<CursorPaginatedResponse<Note>>('/api/v1/notes', { params });
    return response.data;
  },

  async getNote(id: number): Promise<Note> {
    const response = await apiClient.get<Note>(`/api/v1/notes/${id}`);
    return response.data;
  },

  async createNote(data: CreateNoteRequest): Promise<Note> {
    const response = await apiClient.post<Note>('/api/v1/notes', data);
    return response.data;
  },

  async updateNote(id: number, data: UpdateNoteRequest): Promise<Note> {
    const response = await apiClient.put<Note>(`/api/v1/notes/${id}`, data);
    return response.data;
  },

  async deleteNote(id: number): Promise<void> {
    await apiClient.delete(`/api/v1/notes/${id}`);
  }
};
