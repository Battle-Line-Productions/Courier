import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

interface ConnectionFilters {
  search?: string;
  protocol?: string;
  group?: string;
  status?: string;
}

export function useConnections(page: number, pageSize = 10, filters?: ConnectionFilters) {
  return useQuery({
    queryKey: ["connections", page, pageSize, filters],
    queryFn: () => api.listConnections(page, pageSize, filters),
  });
}

export function useConnection(id: string) {
  return useQuery({
    queryKey: ["connections", id],
    queryFn: () => api.getConnection(id),
    enabled: !!id,
  });
}
