import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

export function useNotificationRules(
  page = 1,
  pageSize = 25,
  filters?: { search?: string; entityType?: string; channel?: string; isEnabled?: boolean }
) {
  return useQuery({
    queryKey: ["notification-rules", page, pageSize, filters],
    queryFn: () => api.listNotificationRules({ page, pageSize, ...filters }),
  });
}

export function useNotificationRule(id: string) {
  return useQuery({
    queryKey: ["notification-rules", id],
    queryFn: () => api.getNotificationRule(id),
    enabled: !!id,
  });
}

export function useNotificationLogs(
  page = 1,
  pageSize = 25,
  filters?: { ruleId?: string; entityType?: string; entityId?: string; success?: boolean }
) {
  return useQuery({
    queryKey: ["notification-logs", page, pageSize, filters],
    queryFn: () => api.listNotificationLogs({ page, pageSize, ...filters }),
  });
}
