import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import type { CreateTagRequest, UpdateTagRequest, BulkTagAssignmentRequest } from "../types";

export function useCreateTag() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateTagRequest) => api.createTag(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["tags"] });
    },
  });
}

export function useUpdateTag(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateTagRequest) => api.updateTag(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["tags"] });
    },
  });
}

export function useDeleteTag() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.deleteTag(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["tags"] });
    },
  });
}

export function useAssignTags() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: BulkTagAssignmentRequest) => api.assignTags(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["tags"] });
      queryClient.invalidateQueries({ queryKey: ["jobs"] });
      queryClient.invalidateQueries({ queryKey: ["connections"] });
      queryClient.invalidateQueries({ queryKey: ["pgp-keys"] });
      queryClient.invalidateQueries({ queryKey: ["ssh-keys"] });
      queryClient.invalidateQueries({ queryKey: ["monitors"] });
    },
  });
}

export function useUnassignTags() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: BulkTagAssignmentRequest) => api.unassignTags(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["tags"] });
      queryClient.invalidateQueries({ queryKey: ["jobs"] });
      queryClient.invalidateQueries({ queryKey: ["connections"] });
      queryClient.invalidateQueries({ queryKey: ["pgp-keys"] });
      queryClient.invalidateQueries({ queryKey: ["ssh-keys"] });
      queryClient.invalidateQueries({ queryKey: ["monitors"] });
    },
  });
}
