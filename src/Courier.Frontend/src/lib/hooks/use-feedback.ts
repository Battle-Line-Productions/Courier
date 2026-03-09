import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

export function useFeedbackList(type = "feature", page = 1, pageSize = 20, state = "open") {
  return useQuery({
    queryKey: ["feedback", type, page, pageSize, state],
    queryFn: () => api.listFeedback({ type, page, pageSize, state }),
  });
}

export function useFeedbackItem(number: number) {
  return useQuery({
    queryKey: ["feedback", "item", number],
    queryFn: () => api.getFeedbackItem(number),
    enabled: number > 0,
  });
}
