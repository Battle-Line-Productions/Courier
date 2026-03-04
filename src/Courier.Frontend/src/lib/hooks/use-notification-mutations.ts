import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import type { CreateNotificationRuleRequest, UpdateNotificationRuleRequest } from "../types";

export function useCreateNotificationRule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateNotificationRuleRequest) => api.createNotificationRule(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["notification-rules"] });
    },
  });
}

export function useUpdateNotificationRule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateNotificationRuleRequest }) =>
      api.updateNotificationRule(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["notification-rules"] });
    },
  });
}

export function useDeleteNotificationRule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.deleteNotificationRule(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["notification-rules"] });
    },
  });
}

export function useTestNotificationRule() {
  return useMutation({
    mutationFn: (id: string) => api.testNotificationRule(id),
  });
}
