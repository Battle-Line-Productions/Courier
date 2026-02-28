import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

interface SshKeyFilters {
  search?: string;
  status?: string;
  keyType?: string;
}

export function useSshKeys(page: number, pageSize = 10, filters?: SshKeyFilters) {
  return useQuery({
    queryKey: ["ssh-keys", page, pageSize, filters],
    queryFn: () => api.listSshKeys(page, pageSize, filters),
  });
}

export function useSshKey(id: string) {
  return useQuery({
    queryKey: ["ssh-keys", id],
    queryFn: () => api.getSshKey(id),
    enabled: !!id,
  });
}
