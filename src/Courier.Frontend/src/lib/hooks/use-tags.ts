import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

export function useTags(page = 1, pageSize = 25, filters?: { search?: string; category?: string }) {
  return useQuery({
    queryKey: ["tags", page, pageSize, filters],
    queryFn: () => api.listTags({ page, pageSize, ...filters }),
  });
}

export function useTag(id: string) {
  return useQuery({
    queryKey: ["tags", id],
    queryFn: () => api.getTag(id),
    enabled: !!id,
  });
}

export function useAllTags() {
  return useQuery({
    queryKey: ["tags", "all"],
    queryFn: () => api.listTags({ pageSize: 100 }),
  });
}

export function useTagEntities(id: string, entityType?: string, page = 1, pageSize = 25) {
  return useQuery({
    queryKey: ["tags", id, "entities", entityType, page, pageSize],
    queryFn: () => api.listTagEntities(id, { entityType, page, pageSize }),
    enabled: !!id,
  });
}
