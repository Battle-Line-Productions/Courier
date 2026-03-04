import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import type { CreateJobRequest, UpdateJobRequest, ReplaceJobStepsRequest } from "../types";

export function useCreateJob() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateJobRequest) => api.createJob(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["jobs"] });
    },
  });
}

export function useUpdateJob(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateJobRequest) => api.updateJob(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["jobs"] });
    },
  });
}

export function useDeleteJob() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.deleteJob(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["jobs"] });
    },
  });
}

export function useReplaceSteps(jobId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: ReplaceJobStepsRequest) => api.replaceSteps(jobId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["jobs", jobId, "steps"] });
    },
  });
}

export function useTriggerJob(jobId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.triggerJob(jobId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["jobs", jobId, "executions"] });
    },
  });
}

export function usePauseExecution() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (executionId: string) => api.pauseExecution(executionId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["executions"] });
      queryClient.invalidateQueries({ queryKey: ["jobs"] });
    },
  });
}

export function useResumeExecution() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (executionId: string) => api.resumeExecution(executionId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["executions"] });
      queryClient.invalidateQueries({ queryKey: ["jobs"] });
    },
  });
}

export function useCancelExecution() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ executionId, reason }: { executionId: string; reason?: string }) =>
      api.cancelExecution(executionId, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["executions"] });
      queryClient.invalidateQueries({ queryKey: ["jobs"] });
    },
  });
}
