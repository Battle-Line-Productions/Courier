import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    createNotificationRule: vi.fn(),
    updateNotificationRule: vi.fn(),
    deleteNotificationRule: vi.fn(),
    testNotificationRule: vi.fn(),
  },
}));

import { api } from "../api";
import {
  useCreateNotificationRule,
  useUpdateNotificationRule,
  useDeleteNotificationRule,
  useTestNotificationRule,
} from "./use-notification-mutations";

const mockedApi = api as unknown as {
  createNotificationRule: ReturnType<typeof vi.fn>;
  updateNotificationRule: ReturnType<typeof vi.fn>;
  deleteNotificationRule: ReturnType<typeof vi.fn>;
  testNotificationRule: ReturnType<typeof vi.fn>;
};

let queryClient: QueryClient;

function createWrapper() {
  queryClient = new QueryClient({
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

describe("useCreateNotificationRule", () => {
  it("calls api.createNotificationRule and invalidates notification-rules cache", async () => {
    const newRule = {
      name: "Job Failed Alert",
      channel: "email",
      recipients: ["admin@example.com"],
    };
    const mockResponse = { data: { id: "rule-new", ...newRule } };
    mockedApi.createNotificationRule.mockResolvedValue(mockResponse);

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useCreateNotificationRule(), { wrapper });

    await act(async () => {
      result.current.mutate(newRule as any);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.createNotificationRule).toHaveBeenCalledWith(newRule);
    expect(result.current.data).toEqual(mockResponse);
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["notification-rules"],
    });
  });

  it("sets error state when create fails", async () => {
    mockedApi.createNotificationRule.mockRejectedValue(
      new Error("Validation failed")
    );

    const wrapper = createWrapper();
    const { result } = renderHook(() => useCreateNotificationRule(), { wrapper });

    await act(async () => {
      result.current.mutate({ name: "" } as any);
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.error?.message).toBe("Validation failed");
  });
});

describe("useUpdateNotificationRule", () => {
  it("calls api.updateNotificationRule with id and data, then invalidates cache", async () => {
    const updateData = { name: "Updated Alert", channel: "slack" };
    const mockResponse = { data: { id: "rule-1", ...updateData } };
    mockedApi.updateNotificationRule.mockResolvedValue(mockResponse);

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useUpdateNotificationRule(), { wrapper });

    await act(async () => {
      result.current.mutate({ id: "rule-1", data: updateData as any });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.updateNotificationRule).toHaveBeenCalledWith(
      "rule-1",
      updateData
    );
    expect(result.current.data).toEqual(mockResponse);
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["notification-rules"],
    });
  });
});

describe("useDeleteNotificationRule", () => {
  it("calls api.deleteNotificationRule and invalidates notification-rules cache", async () => {
    mockedApi.deleteNotificationRule.mockResolvedValue({ data: null });

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useDeleteNotificationRule(), { wrapper });

    await act(async () => {
      result.current.mutate("rule-to-delete");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.deleteNotificationRule).toHaveBeenCalledWith("rule-to-delete");
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["notification-rules"],
    });
  });

  it("sets error state when delete fails", async () => {
    mockedApi.deleteNotificationRule.mockRejectedValue(new Error("Not found"));

    const wrapper = createWrapper();
    const { result } = renderHook(() => useDeleteNotificationRule(), { wrapper });

    await act(async () => {
      result.current.mutate("nonexistent-id");
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe("Not found");
  });
});

describe("useTestNotificationRule", () => {
  it("calls api.testNotificationRule with the rule id", async () => {
    const mockResult = {
      data: { success: true, message: "Notification sent" },
    };
    mockedApi.testNotificationRule.mockResolvedValue(mockResult);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useTestNotificationRule(), { wrapper });

    await act(async () => {
      result.current.mutate("rule-1");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.testNotificationRule).toHaveBeenCalledWith("rule-1");
    expect(result.current.data).toEqual(mockResult);
  });

  it("does not invalidate notification-rules cache on success", async () => {
    mockedApi.testNotificationRule.mockResolvedValue({
      data: { success: true },
    });

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useTestNotificationRule(), { wrapper });

    await act(async () => {
      result.current.mutate("rule-1");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidateSpy).not.toHaveBeenCalled();
  });
});
