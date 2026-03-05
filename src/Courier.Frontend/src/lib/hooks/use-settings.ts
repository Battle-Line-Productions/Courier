import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import type { UpdateAuthSettingsRequest } from "../types";

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
