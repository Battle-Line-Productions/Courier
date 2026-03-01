import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

export function useAzureFunctionTraces(connectionId: string, invocationId: string) {
  return useQuery({
    queryKey: ["azure-function-traces", connectionId, invocationId],
    queryFn: () => api.getAzureFunctionTraces(connectionId, invocationId),
    enabled: !!connectionId && !!invocationId,
  });
}
