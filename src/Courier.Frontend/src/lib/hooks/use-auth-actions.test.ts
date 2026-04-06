import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    changePassword: vi.fn(),
  },
}));

import { api } from "../api";
import { useChangePassword } from "./use-auth-actions";

const mockedApi = api as unknown as {
  changePassword: ReturnType<typeof vi.fn>;
};

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: queryClient }, children);
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("useChangePassword", () => {
  it("calls api.changePassword with the provided data", async () => {
    const passwordData = { currentPassword: "old123", newPassword: "new456" };
    mockedApi.changePassword.mockResolvedValue({ data: null });

    const { result } = renderHook(() => useChangePassword(), {
      wrapper: createWrapper(),
    });

    await act(async () => {
      result.current.mutate(passwordData as any);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.changePassword).toHaveBeenCalledWith(passwordData);
  });

  it("sets error state when change password fails", async () => {
    mockedApi.changePassword.mockRejectedValue(new Error("Current password incorrect"));

    const { result } = renderHook(() => useChangePassword(), {
      wrapper: createWrapper(),
    });

    await act(async () => {
      result.current.mutate({ currentPassword: "wrong", newPassword: "new456" } as any);
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe("Current password incorrect");
  });
});
