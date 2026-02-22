import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

export function useJobExecutions(jobId: string, page = 1, pageSize = 10) {
  return useQuery({
    queryKey: ["jobs", jobId, "executions", page, pageSize],
    queryFn: () => api.listExecutions(jobId, page, pageSize),
    enabled: !!jobId,
  });
}

export function useExecution(executionId: string, enabled = true) {
  return useQuery({
    queryKey: ["executions", executionId],
    queryFn: () => api.getExecution(executionId),
    enabled: !!executionId && enabled,
    refetchInterval: (query) => {
      const state = query.state.data?.data?.state;
      if (state === "queued" || state === "running") {
        return 2000;
      }
      return false;
    },
  });
}
