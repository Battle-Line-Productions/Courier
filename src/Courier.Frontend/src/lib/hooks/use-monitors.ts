import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

interface MonitorFilters {
  search?: string;
  state?: string;
}

export function useMonitors(page: number, pageSize = 10, filters?: MonitorFilters) {
  return useQuery({
    queryKey: ["monitors", page, pageSize, filters],
    queryFn: () => api.listMonitors(page, pageSize, filters),
  });
}

export function useMonitor(id: string) {
  return useQuery({
    queryKey: ["monitors", id],
    queryFn: () => api.getMonitor(id),
    enabled: !!id,
  });
}

export function useMonitorFileLog(monitorId: string, page = 1, pageSize = 25) {
  return useQuery({
    queryKey: ["monitors", monitorId, "file-log", page, pageSize],
    queryFn: () => api.listMonitorFileLog(monitorId, page, pageSize),
    enabled: !!monitorId,
  });
}
