import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import type { GenerateSshKeyRequest, UpdateSshKeyRequest } from "../types";

export function useGenerateSshKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: GenerateSshKeyRequest) => api.generateSshKey(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["ssh-keys"] });
    },
  });
}

export function useImportSshKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (formData: FormData) => api.importSshKey(formData),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["ssh-keys"] });
    },
  });
}

export function useUpdateSshKey(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateSshKeyRequest) => api.updateSshKey(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["ssh-keys"] });
    },
  });
}

export function useDeleteSshKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.deleteSshKey(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["ssh-keys"] });
    },
  });
}

export function useRetireSshKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.retireSshKey(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["ssh-keys"] });
    },
  });
}

export function useActivateSshKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.activateSshKey(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["ssh-keys"] });
    },
  });
}
