import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { api } from "../api";

interface AuditLogFilters {
  entityType?: string;
  operation?: string;
  performedBy?: string;
  from?: string;
  to?: string;
}

export function useAuditLog(page: number, pageSize = 25, filters?: AuditLogFilters) {
  return useQuery({
    queryKey: ["audit-log", page, pageSize, filters],
    queryFn: () => api.listAuditLog(page, pageSize, filters),
    placeholderData: keepPreviousData,
  });
}

export function useEntityAuditLog(entityType: string, entityId: string, page = 1, pageSize = 25) {
  return useQuery({
    queryKey: ["audit-log", "entity", entityType, entityId, page, pageSize],
    queryFn: () => api.listAuditLogByEntity(entityType, entityId, page, pageSize),
    enabled: !!entityType && !!entityId,
    placeholderData: keepPreviousData,
  });
}
