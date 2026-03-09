import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import type { CreateFeedbackRequest } from "../types";

export function useCreateFeedback() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateFeedbackRequest) => api.createFeedback(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["feedback"] });
    },
  });
}

export function useVoteFeedback() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (number: number) => api.voteFeedback(number),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["feedback"] });
    },
  });
}

export function useUnvoteFeedback() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (number: number) => api.unvoteFeedback(number),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["feedback"] });
    },
  });
}

export function useLinkGitHub() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (code: string) => api.linkGitHubAccount(code),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["auth"] });
      queryClient.invalidateQueries({ queryKey: ["users"] });
    },
  });
}

export function useUnlinkGitHub() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.unlinkGitHubAccount(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["auth"] });
      queryClient.invalidateQueries({ queryKey: ["users"] });
    },
  });
}
