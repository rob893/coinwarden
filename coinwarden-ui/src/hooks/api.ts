import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { helloApi, notesApi } from '../services/api';
import { useAuth } from './useAuth';
import type { CreateNoteRequest } from '../types/models';

export const queryKeys = {
  helloV1: ['hello', 'v1'] as const,
  helloV2: ['hello', 'v2'] as const,
  notesInfinite: (pageSize: number) => ['notes', 'infinite', pageSize] as const,
  note: (id: number) => ['notes', id] as const
} as const;

// Hello hooks
export function useHelloV1() {
  const { isLoading: isAuthLoading, isAuthenticated } = useAuth();

  return useQuery({
    queryKey: queryKeys.helloV1,
    queryFn: () => helloApi.getHelloV1(),
    enabled: isAuthenticated && !isAuthLoading,
    staleTime: 60 * 1000
  });
}

export function useHelloV2() {
  const { isLoading: isAuthLoading, isAuthenticated } = useAuth();

  return useQuery({
    queryKey: queryKeys.helloV2,
    queryFn: () => helloApi.getHelloV2(),
    enabled: isAuthenticated && !isAuthLoading,
    staleTime: 60 * 1000
  });
}

// Notes hooks
export function useCreateNote() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateNoteRequest) => notesApi.createNote(data),
    onSuccess: newNote => {
      queryClient.invalidateQueries({ queryKey: ['notes'] });
      queryClient.setQueryData(queryKeys.note(newNote.id), newNote);
    }
  });
}

export function useDeleteNote() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: number) => notesApi.deleteNote(id),
    onSuccess: (_data, id) => {
      queryClient.invalidateQueries({ queryKey: ['notes'] });
      queryClient.removeQueries({ queryKey: queryKeys.note(id) });
    }
  });
}

// Cursor-paginated notes with "load more" support, cached via React Query's
// infinite query. Created/deleted notes invalidate the `['notes']` key, which
// also refreshes this query so the list stays in sync.
export function useInfiniteNotes(pageSize = 10) {
  const { isLoading: isAuthLoading, isAuthenticated } = useAuth();

  return useInfiniteQuery({
    queryKey: queryKeys.notesInfinite(pageSize),
    queryFn: ({ pageParam }: { pageParam: string | undefined }) =>
      notesApi.getNotes({ first: pageSize, after: pageParam }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: lastPage => (lastPage.pageInfo?.hasNextPage ? lastPage.pageInfo.endCursor : undefined),
    enabled: isAuthenticated && !isAuthLoading,
    staleTime: 5 * 60 * 1000
  });
}
