import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import type { CreateConnectionRequest, UpdateConnectionRequest } from "../types";

export function useCreateConnection() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateConnectionRequest) => api.createConnection(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["connections"] });
    },
  });
}

export function useUpdateConnection(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateConnectionRequest) => api.updateConnection(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["connections"] });
    },
  });
}

export function useDeleteConnection() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.deleteConnection(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["connections"] });
    },
  });
}

export function useTestConnection() {
  return useMutation({
    mutationFn: (id: string) => api.testConnection(id),
  });
}
