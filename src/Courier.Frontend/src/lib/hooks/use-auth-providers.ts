import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import type { CreateAuthProviderRequest, UpdateAuthProviderRequest } from "../types";

export function useAuthProviders(page = 1, pageSize = 25) {
  return useQuery({
    queryKey: ["auth-providers", page, pageSize],
    queryFn: () => api.listAuthProviders(page, pageSize),
  });
}

export function useAuthProvider(id: string) {
  return useQuery({
    queryKey: ["auth-providers", id],
    queryFn: () => api.getAuthProvider(id),
    enabled: !!id,
  });
}

export function useLoginOptions() {
  return useQuery({
    queryKey: ["login-options"],
    queryFn: () => api.getLoginOptions(),
    staleTime: 60_000,
  });
}

export function useCreateAuthProvider() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateAuthProviderRequest) => api.createAuthProvider(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["auth-providers"] });
    },
  });
}

export function useUpdateAuthProvider() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateAuthProviderRequest }) =>
      api.updateAuthProvider(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["auth-providers"] });
    },
  });
}

export function useDeleteAuthProvider() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.deleteAuthProvider(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["auth-providers"] });
    },
  });
}

export function useTestAuthProvider() {
  return useMutation({
    mutationFn: (id: string) => api.testAuthProvider(id),
  });
}
