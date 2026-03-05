import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import type { CreateChainScheduleRequest, UpdateChainScheduleRequest } from "../types";

export function useChainSchedules(chainId: string) {
  return useQuery({
    queryKey: ["chains", chainId, "schedules"],
    queryFn: () => api.listChainSchedules(chainId),
    enabled: !!chainId,
  });
}

export function useCreateChainSchedule(chainId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateChainScheduleRequest) => api.createChainSchedule(chainId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["chains", chainId, "schedules"] });
    },
  });
}

export function useUpdateChainSchedule(chainId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ scheduleId, data }: { scheduleId: string; data: UpdateChainScheduleRequest }) =>
      api.updateChainSchedule(chainId, scheduleId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["chains", chainId, "schedules"] });
    },
  });
}

export function useDeleteChainSchedule(chainId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (scheduleId: string) => api.deleteChainSchedule(chainId, scheduleId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["chains", chainId, "schedules"] });
    },
  });
}
