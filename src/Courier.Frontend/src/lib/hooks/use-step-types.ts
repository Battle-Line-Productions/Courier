import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

export function useStepTypes() {
  return useQuery({
    queryKey: ["step-types"],
    queryFn: () => api.listStepTypes(),
    staleTime: Infinity,
  });
}
