import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

export function useJobSteps(jobId: string) {
  return useQuery({
    queryKey: ["jobs", jobId, "steps"],
    queryFn: () => api.listSteps(jobId),
    enabled: !!jobId,
  });
}
