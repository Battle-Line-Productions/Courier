import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import type { CreateJobScheduleRequest, UpdateJobScheduleRequest } from "../types";

export function useJobSchedules(jobId: string) {
  return useQuery({
    queryKey: ["jobs", jobId, "schedules"],
    queryFn: () => api.listSchedules(jobId),
    enabled: !!jobId,
  });
}

export function useCreateSchedule(jobId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateJobScheduleRequest) => api.createSchedule(jobId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["jobs", jobId, "schedules"] });
    },
  });
}

export function useUpdateSchedule(jobId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ scheduleId, data }: { scheduleId: string; data: UpdateJobScheduleRequest }) =>
      api.updateSchedule(jobId, scheduleId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["jobs", jobId, "schedules"] });
    },
  });
}

export function useDeleteSchedule(jobId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (scheduleId: string) => api.deleteSchedule(jobId, scheduleId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["jobs", jobId, "schedules"] });
    },
  });
}
