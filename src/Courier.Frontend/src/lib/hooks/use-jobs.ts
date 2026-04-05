import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

interface JobFilters {
  search?: string;
  tag?: string;
}

export function useJobs(page: number, pageSize = 10, filters?: JobFilters) {
  return useQuery({
    queryKey: ["jobs", page, pageSize, filters],
    queryFn: () => api.listJobs(page, pageSize, filters),
  });
}

export function useJob(id: string) {
  return useQuery({
    queryKey: ["jobs", id],
    queryFn: () => api.getJob(id),
    enabled: !!id,
  });
}

export function useAllJobs() {
  return useQuery({
    queryKey: ["jobs", "all"],
    queryFn: () => api.listJobs(1, 200),
  });
}
