import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import type { GeneratePgpKeyRequest, UpdatePgpKeyRequest } from "../types";

export function useGeneratePgpKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: GeneratePgpKeyRequest) => api.generatePgpKey(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["pgp-keys"] });
    },
  });
}

export function useImportPgpKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (formData: FormData) => api.importPgpKey(formData),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["pgp-keys"] });
    },
  });
}

export function useUpdatePgpKey(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdatePgpKeyRequest) => api.updatePgpKey(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["pgp-keys"] });
    },
  });
}

export function useDeletePgpKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.deletePgpKey(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["pgp-keys"] });
    },
  });
}

export function useRetirePgpKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.retirePgpKey(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["pgp-keys"] });
    },
  });
}

export function useRevokePgpKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.revokePgpKey(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["pgp-keys"] });
    },
  });
}

export function useActivatePgpKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.activatePgpKey(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["pgp-keys"] });
    },
  });
}
