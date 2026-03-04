import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import type {
  CreateChainRequest,
  UpdateChainRequest,
  ReplaceChainMembersRequest,
} from "../types";

export function useCreateChain() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateChainRequest) => api.createChain(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["chains"] });
    },
  });
}

export function useUpdateChain(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateChainRequest) => api.updateChain(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["chains"] });
    },
  });
}

export function useDeleteChain() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.deleteChain(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["chains"] });
    },
  });
}

export function useReplaceChainMembers(chainId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: ReplaceChainMembersRequest) =>
      api.replaceChainMembers(chainId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["chains", chainId] });
    },
  });
}

export function useTriggerChain(chainId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.triggerChain(chainId),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ["chains", chainId, "executions"],
      });
    },
  });
}
