import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    getDashboardSummary: vi.fn(),
    getRecentExecutions: vi.fn(),
    getActiveMonitors: vi.fn(),
    getExpiringKeys: vi.fn(),
  },
}));

import { api } from "../api";
import {
  useDashboardSummary,
  useRecentExecutions,
  useActiveMonitors,
  useExpiringKeys,
} from "./use-dashboard";

const mockedApi = api as unknown as {
  getDashboardSummary: ReturnType<typeof vi.fn>;
  getRecentExecutions: ReturnType<typeof vi.fn>;
  getActiveMonitors: ReturnType<typeof vi.fn>;
  getExpiringKeys: ReturnType<typeof vi.fn>;
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

describe("useDashboardSummary", () => {
  it("calls api.getDashboardSummary and returns data", async () => {
    const mockSummary = {
      data: {
        totalJobs: 12,
        activeMonitors: 5,
        recentExecutions: 42,
        failedExecutions: 3,
      },
    };
    mockedApi.getDashboardSummary.mockResolvedValue(mockSummary);

    const { result } = renderHook(() => useDashboardSummary(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.getDashboardSummary).toHaveBeenCalledTimes(1);
    expect(result.current.data).toEqual(mockSummary);
  });

  it("returns error state when api call fails", async () => {
    mockedApi.getDashboardSummary.mockRejectedValue(new Error("Server error"));

    const { result } = renderHook(() => useDashboardSummary(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.error?.message).toBe("Server error");
  });
});

describe("useRecentExecutions", () => {
  it("calls api.getRecentExecutions with default count", async () => {
    const mockExecutions = {
      data: [
        { id: "exec-1", jobName: "Daily Report", status: "completed" },
        { id: "exec-2", jobName: "Nightly Sync", status: "failed" },
      ],
    };
    mockedApi.getRecentExecutions.mockResolvedValue(mockExecutions);

    const { result } = renderHook(() => useRecentExecutions(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.getRecentExecutions).toHaveBeenCalledWith(10);
    expect(result.current.data).toEqual(mockExecutions);
  });

  it("calls api.getRecentExecutions with custom count", async () => {
    const mockExecutions = { data: [] };
    mockedApi.getRecentExecutions.mockResolvedValue(mockExecutions);

    const { result } = renderHook(() => useRecentExecutions(5), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.getRecentExecutions).toHaveBeenCalledWith(5);
  });
});

describe("useActiveMonitors", () => {
  it("calls api.getActiveMonitors and returns data", async () => {
    const mockMonitors = {
      data: [
        { id: "mon-1", name: "Upload Watcher", state: "active" },
        { id: "mon-2", name: "Report Monitor", state: "active" },
      ],
    };
    mockedApi.getActiveMonitors.mockResolvedValue(mockMonitors);

    const { result } = renderHook(() => useActiveMonitors(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.getActiveMonitors).toHaveBeenCalledTimes(1);
    expect(result.current.data).toEqual(mockMonitors);
  });
});

describe("useExpiringKeys", () => {
  it("calls api.getExpiringKeys with default daysAhead", async () => {
    const mockKeys = {
      data: [
        { id: "key-1", name: "PGP Key", expiresAt: "2026-05-01T00:00:00Z" },
      ],
    };
    mockedApi.getExpiringKeys.mockResolvedValue(mockKeys);

    const { result } = renderHook(() => useExpiringKeys(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.getExpiringKeys).toHaveBeenCalledWith(30);
    expect(result.current.data).toEqual(mockKeys);
  });

  it("calls api.getExpiringKeys with custom daysAhead", async () => {
    const mockKeys = { data: [] };
    mockedApi.getExpiringKeys.mockResolvedValue(mockKeys);

    const { result } = renderHook(() => useExpiringKeys(7), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.getExpiringKeys).toHaveBeenCalledWith(7);
  });
});
