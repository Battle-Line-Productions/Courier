import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    listAuditLog: vi.fn(),
    listAuditLogByEntity: vi.fn(),
  },
}));

import { api } from "../api";
import { useAuditLog, useEntityAuditLog } from "./use-audit-log";

const mockedApi = api as unknown as {
  listAuditLog: ReturnType<typeof vi.fn>;
  listAuditLogByEntity: ReturnType<typeof vi.fn>;
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

describe("useAuditLog", () => {
  it("calls api.listAuditLog with page, default pageSize, and no filters", async () => {
    const mockResponse = { data: [], totalCount: 0, page: 1, pageSize: 25 };
    mockedApi.listAuditLog.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useAuditLog(1), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listAuditLog).toHaveBeenCalledWith(1, 25, undefined);
    expect(result.current.data).toEqual(mockResponse);
  });

  it("passes filters to api.listAuditLog", async () => {
    const filters = { entityType: "job", operation: "create", performedBy: "admin" };
    const mockResponse = {
      data: [{ id: "al-1", entityType: "job", operation: "create" }],
      totalCount: 1,
      page: 1,
      pageSize: 25,
    };
    mockedApi.listAuditLog.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useAuditLog(1, 25, filters), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listAuditLog).toHaveBeenCalledWith(1, 25, filters);
    expect(result.current.data).toEqual(mockResponse);
  });

  it("returns error state when api call fails", async () => {
    mockedApi.listAuditLog.mockRejectedValue(new Error("Unauthorized"));

    const { result } = renderHook(() => useAuditLog(1), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe("Unauthorized");
  });
});

describe("useEntityAuditLog", () => {
  it("calls api.listAuditLogByEntity when entityType and entityId are provided", async () => {
    const mockResponse = {
      data: [{ id: "al-2", entityType: "job", entityId: "job-1" }],
      totalCount: 1,
      page: 1,
      pageSize: 25,
    };
    mockedApi.listAuditLogByEntity.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useEntityAuditLog("job", "job-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listAuditLogByEntity).toHaveBeenCalledWith("job", "job-1", 1, 25);
    expect(result.current.data).toEqual(mockResponse);
  });

  it("does not fetch when entityType is empty", async () => {
    const { result } = renderHook(() => useEntityAuditLog("", "job-1"), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe("idle");
    expect(mockedApi.listAuditLogByEntity).not.toHaveBeenCalled();
  });

  it("does not fetch when entityId is empty", async () => {
    const { result } = renderHook(() => useEntityAuditLog("job", ""), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe("idle");
    expect(mockedApi.listAuditLogByEntity).not.toHaveBeenCalled();
  });
});
