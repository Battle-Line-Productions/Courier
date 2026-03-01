import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

export function useDashboardSummary() {
  return useQuery({
    queryKey: ["dashboard", "summary"],
    queryFn: () => api.getDashboardSummary(),
    refetchInterval: 30_000,
  });
}

export function useRecentExecutions(count = 10) {
  return useQuery({
    queryKey: ["dashboard", "recent-executions", count],
    queryFn: () => api.getRecentExecutions(count),
    refetchInterval: 15_000,
  });
}

export function useActiveMonitors() {
  return useQuery({
    queryKey: ["dashboard", "active-monitors"],
    queryFn: () => api.getActiveMonitors(),
    refetchInterval: 30_000,
  });
}

export function useExpiringKeys(daysAhead = 30) {
  return useQuery({
    queryKey: ["dashboard", "key-expiry", daysAhead],
    queryFn: () => api.getExpiringKeys(daysAhead),
    refetchInterval: 60_000,
  });
}
