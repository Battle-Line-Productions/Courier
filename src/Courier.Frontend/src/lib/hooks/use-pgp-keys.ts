import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

interface PgpKeyFilters {
  search?: string;
  status?: string;
  keyType?: string;
  algorithm?: string;
}

export function usePgpKeys(page: number, pageSize = 10, filters?: PgpKeyFilters) {
  return useQuery({
    queryKey: ["pgp-keys", page, pageSize, filters],
    queryFn: () => api.listPgpKeys(page, pageSize, filters),
  });
}

export function usePgpKey(id: string) {
  return useQuery({
    queryKey: ["pgp-keys", id],
    queryFn: () => api.getPgpKey(id),
    enabled: !!id,
  });
}
