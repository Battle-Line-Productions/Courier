import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

interface ChainFilters {
  search?: string;
  tag?: string;
}

export function useChains(page = 1, pageSize = 10, filters?: ChainFilters) {
  return useQuery({
    queryKey: ["chains", page, pageSize, filters],
    queryFn: () => api.listChains(page, pageSize, filters),
  });
}

export function useChain(id: string) {
  return useQuery({
    queryKey: ["chains", id],
    queryFn: () => api.getChain(id),
    enabled: !!id,
  });
}

export function useChainExecutions(chainId: string, page = 1, pageSize = 10) {
  return useQuery({
    queryKey: ["chains", chainId, "executions", page, pageSize],
    queryFn: () => api.listChainExecutions(chainId, page, pageSize),
    enabled: !!chainId,
  });
}
