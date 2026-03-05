import { useMutation } from "@tanstack/react-query";
import { api } from "../api";
import type { ChangePasswordRequest } from "../types";

export function useChangePassword() {
  return useMutation({
    mutationFn: (data: ChangePasswordRequest) => api.changePassword(data),
  });
}
