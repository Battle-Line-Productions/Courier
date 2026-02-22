import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

export function useJobs(page: number, pageSize = 10) {
  return useQuery({
    queryKey: ["jobs", page, pageSize],
    queryFn: () => api.listJobs(page, pageSize),
  });
}

export function useJob(id: string) {
  return useQuery({
    queryKey: ["jobs", id],
    queryFn: () => api.getJob(id),
    enabled: !!id,
  });
}
