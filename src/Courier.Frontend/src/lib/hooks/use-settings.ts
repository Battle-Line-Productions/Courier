import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import type { UpdateAuthSettingsRequest, UpdateSmtpSettingsRequest } from "../types";

export function useAuthSettings() {
  return useQuery({
    queryKey: ["settings", "auth"],
    queryFn: () => api.getAuthSettings(),
  });
}

export function useUpdateAuthSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateAuthSettingsRequest) => api.updateAuthSettings(data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["settings", "auth"] }),
  });
}

export function useSmtpSettings() {
  return useQuery({
    queryKey: ["settings", "smtp"],
    queryFn: () => api.getSmtpSettings(),
  });
}

export function useUpdateSmtpSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateSmtpSettingsRequest) => api.updateSmtpSettings(data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["settings", "smtp"] }),
  });
}

export function useTestSmtpConnection() {
  return useMutation({
    mutationFn: () => api.testSmtpConnection(),
  });
}
