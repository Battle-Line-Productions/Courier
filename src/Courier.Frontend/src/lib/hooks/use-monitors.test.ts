import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    listMonitors: vi.fn(),
    getMonitor: vi.fn(),
    listMonitorFileLog: vi.fn(),
  },
}));

import { api } from "../api";
import { useMonitors, useMonitor, useMonitorFileLog } from "./use-monitors";

const mockedApi = api as unknown as {
  listMonitors: ReturnType<typeof vi.fn>;
  getMonitor: ReturnType<typeof vi.fn>;
  listMonitorFileLog: ReturnType<typeof vi.fn>;
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

describe("useMonitors", () => {
  it("calls api.listMonitors with page and default pageSize", async () => {
    const mockResponse = {
      data: [],
      totalCount: 0,
      page: 1,
      pageSize: 10,
    };
    mockedApi.listMonitors.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useMonitors(1), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listMonitors).toHaveBeenCalledWith(1, 10, undefined);
    expect(result.current.data).toEqual(mockResponse);
  });

  it("calls api.listMonitors with custom pageSize", async () => {
    const mockResponse = {
      data: [],
      totalCount: 0,
      page: 2,
      pageSize: 25,
    };
    mockedApi.listMonitors.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useMonitors(2, 25), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listMonitors).toHaveBeenCalledWith(2, 25, undefined);
    expect(result.current.data).toEqual(mockResponse);
  });

  it("passes filters to api.listMonitors", async () => {
    const filters = { search: "uploads", state: "active", tag: "prod" };
    const mockResponse = {
      data: [{ id: "mon-1", name: "Upload Monitor" }],
      totalCount: 1,
      page: 1,
      pageSize: 10,
    };
    mockedApi.listMonitors.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useMonitors(1, 10, filters), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listMonitors).toHaveBeenCalledWith(1, 10, filters);
    expect(result.current.data).toEqual(mockResponse);
  });

  it("returns error state when api call fails", async () => {
    mockedApi.listMonitors.mockRejectedValue(new Error("Network error"));

    const { result } = renderHook(() => useMonitors(1), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.error?.message).toBe("Network error");
  });
});

describe("useMonitor", () => {
  it("calls api.getMonitor with the provided id", async () => {
    const mockMonitor = {
      data: { id: "mon-1", name: "Upload Watcher", state: "active" },
    };
    mockedApi.getMonitor.mockResolvedValue(mockMonitor);

    const { result } = renderHook(() => useMonitor("mon-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.getMonitor).toHaveBeenCalledWith("mon-1");
    expect(result.current.data).toEqual(mockMonitor);
  });

  it("does not fetch when id is an empty string", async () => {
    const { result } = renderHook(() => useMonitor(""), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe("idle");
    expect(mockedApi.getMonitor).not.toHaveBeenCalled();
  });
});

describe("useMonitorFileLog", () => {
  it("calls api.listMonitorFileLog with defaults", async () => {
    const mockResponse = {
      data: [{ id: "log-1", fileName: "report.csv" }],
      totalCount: 1,
      page: 1,
      pageSize: 25,
    };
    mockedApi.listMonitorFileLog.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useMonitorFileLog("mon-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listMonitorFileLog).toHaveBeenCalledWith("mon-1", 1, 25);
    expect(result.current.data).toEqual(mockResponse);
  });

  it("calls api.listMonitorFileLog with custom page and pageSize", async () => {
    const mockResponse = {
      data: [],
      totalCount: 50,
      page: 3,
      pageSize: 10,
    };
    mockedApi.listMonitorFileLog.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useMonitorFileLog("mon-1", 3, 10), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listMonitorFileLog).toHaveBeenCalledWith("mon-1", 3, 10);
    expect(result.current.data).toEqual(mockResponse);
  });

  it("does not fetch when monitorId is an empty string", async () => {
    const { result } = renderHook(() => useMonitorFileLog(""), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe("idle");
    expect(mockedApi.listMonitorFileLog).not.toHaveBeenCalled();
  });
});
