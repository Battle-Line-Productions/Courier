import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    createMonitor: vi.fn(),
    updateMonitor: vi.fn(),
    deleteMonitor: vi.fn(),
    activateMonitor: vi.fn(),
    pauseMonitor: vi.fn(),
    disableMonitor: vi.fn(),
    acknowledgeMonitorError: vi.fn(),
  },
}));

import { api } from "../api";
import {
  useCreateMonitor,
  useUpdateMonitor,
  useDeleteMonitor,
  useActivateMonitor,
  usePauseMonitor,
  useDisableMonitor,
  useAcknowledgeMonitorError,
} from "./use-monitor-mutations";

const mockedApi = api as unknown as {
  createMonitor: ReturnType<typeof vi.fn>;
  updateMonitor: ReturnType<typeof vi.fn>;
  deleteMonitor: ReturnType<typeof vi.fn>;
  activateMonitor: ReturnType<typeof vi.fn>;
  pauseMonitor: ReturnType<typeof vi.fn>;
  disableMonitor: ReturnType<typeof vi.fn>;
  acknowledgeMonitorError: ReturnType<typeof vi.fn>;
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

describe("useCreateMonitor", () => {
  it("calls api.createMonitor and invalidates monitors cache", async () => {
    const newMonitor = {
      name: "Upload Watcher",
      connectionId: "conn-1",
      path: "/uploads",
      pattern: "*.csv",
    };
    const mockResponse = {
      data: { id: "mon-new", ...newMonitor },
    };
    mockedApi.createMonitor.mockResolvedValue(mockResponse);

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useCreateMonitor(), { wrapper });

    await act(async () => {
      result.current.mutate(newMonitor as any);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.createMonitor).toHaveBeenCalledWith(newMonitor);
    expect(result.current.data).toEqual(mockResponse);
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["monitors"],
    });
  });

  it("sets error state when create fails", async () => {
    mockedApi.createMonitor.mockRejectedValue(new Error("Validation failed"));

    const wrapper = createWrapper();
    const { result } = renderHook(() => useCreateMonitor(), { wrapper });

    await act(async () => {
      result.current.mutate({ name: "" } as any);
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.error?.message).toBe("Validation failed");
  });
});

describe("useUpdateMonitor", () => {
  it("calls api.updateMonitor with id and data, then invalidates cache", async () => {
    const updateData = {
      name: "Updated Watcher",
      path: "/uploads/v2",
    };
    const mockResponse = {
      data: { id: "mon-1", ...updateData },
    };
    mockedApi.updateMonitor.mockResolvedValue(mockResponse);

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useUpdateMonitor("mon-1"), {
      wrapper,
    });

    await act(async () => {
      result.current.mutate(updateData as any);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.updateMonitor).toHaveBeenCalledWith("mon-1", updateData);
    expect(result.current.data).toEqual(mockResponse);
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["monitors"],
    });
  });
});

describe("useDeleteMonitor", () => {
  it("calls api.deleteMonitor and invalidates monitors cache", async () => {
    mockedApi.deleteMonitor.mockResolvedValue({ data: null });

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useDeleteMonitor(), { wrapper });

    await act(async () => {
      result.current.mutate("mon-to-delete");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.deleteMonitor).toHaveBeenCalledWith("mon-to-delete");
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["monitors"],
    });
  });

  it("sets error state when delete fails", async () => {
    mockedApi.deleteMonitor.mockRejectedValue(new Error("Not found"));

    const wrapper = createWrapper();
    const { result } = renderHook(() => useDeleteMonitor(), { wrapper });

    await act(async () => {
      result.current.mutate("nonexistent-id");
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe("Not found");
  });
});

describe("useActivateMonitor", () => {
  it("calls api.activateMonitor and invalidates monitors cache", async () => {
    mockedApi.activateMonitor.mockResolvedValue({ data: { id: "mon-1", state: "active" } });

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useActivateMonitor(), { wrapper });

    await act(async () => {
      result.current.mutate("mon-1");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.activateMonitor).toHaveBeenCalledWith("mon-1");
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["monitors"],
    });
  });
});

describe("usePauseMonitor", () => {
  it("calls api.pauseMonitor and invalidates monitors cache", async () => {
    mockedApi.pauseMonitor.mockResolvedValue({ data: { id: "mon-1", state: "paused" } });

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => usePauseMonitor(), { wrapper });

    await act(async () => {
      result.current.mutate("mon-1");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.pauseMonitor).toHaveBeenCalledWith("mon-1");
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["monitors"],
    });
  });
});

describe("useDisableMonitor", () => {
  it("calls api.disableMonitor and invalidates monitors cache", async () => {
    mockedApi.disableMonitor.mockResolvedValue({ data: { id: "mon-1", state: "disabled" } });

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useDisableMonitor(), { wrapper });

    await act(async () => {
      result.current.mutate("mon-1");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.disableMonitor).toHaveBeenCalledWith("mon-1");
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["monitors"],
    });
  });

  it("sets error state when disable fails", async () => {
    mockedApi.disableMonitor.mockRejectedValue(new Error("Cannot disable"));

    const wrapper = createWrapper();
    const { result } = renderHook(() => useDisableMonitor(), { wrapper });

    await act(async () => {
      result.current.mutate("mon-1");
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.error?.message).toBe("Cannot disable");
  });
});

describe("useAcknowledgeMonitorError", () => {
  it("calls api.acknowledgeMonitorError and invalidates monitors cache", async () => {
    mockedApi.acknowledgeMonitorError.mockResolvedValue({ data: { id: "mon-1", state: "active" } });

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useAcknowledgeMonitorError(), { wrapper });

    await act(async () => {
      result.current.mutate("mon-1");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.acknowledgeMonitorError).toHaveBeenCalledWith("mon-1");
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["monitors"],
    });
  });

  it("sets error state when acknowledge fails", async () => {
    mockedApi.acknowledgeMonitorError.mockRejectedValue(new Error("Not found"));

    const wrapper = createWrapper();
    const { result } = renderHook(() => useAcknowledgeMonitorError(), { wrapper });

    await act(async () => {
      result.current.mutate("nonexistent-id");
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.error?.message).toBe("Not found");
  });
});
