import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    listConnections: vi.fn(),
    getConnection: vi.fn(),
  },
}));

import { api } from "../api";
import { useConnections, useConnection } from "./use-connections";

const mockedApi = api as unknown as {
  listConnections: ReturnType<typeof vi.fn>;
  getConnection: ReturnType<typeof vi.fn>;
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

describe("useConnections", () => {
  it("calls api.listConnections with page and default pageSize", async () => {
    const mockResponse = {
      data: [],
      totalCount: 0,
      page: 1,
      pageSize: 10,
    };
    mockedApi.listConnections.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useConnections(1), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listConnections).toHaveBeenCalledWith(1, 10, undefined);
    expect(result.current.data).toEqual(mockResponse);
  });

  it("calls api.listConnections with custom pageSize", async () => {
    const mockResponse = {
      data: [],
      totalCount: 0,
      page: 2,
      pageSize: 25,
    };
    mockedApi.listConnections.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useConnections(2, 25), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listConnections).toHaveBeenCalledWith(2, 25, undefined);
    expect(result.current.data).toEqual(mockResponse);
  });

  it("passes filters to api.listConnections", async () => {
    const filters = { search: "sftp", protocol: "sftp", group: "prod" };
    const mockResponse = {
      data: [{ id: "abc", name: "SFTP Prod" }],
      totalCount: 1,
      page: 1,
      pageSize: 10,
    };
    mockedApi.listConnections.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useConnections(1, 10, filters), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listConnections).toHaveBeenCalledWith(1, 10, filters);
    expect(result.current.data).toEqual(mockResponse);
  });

  it("returns error state when api call fails", async () => {
    mockedApi.listConnections.mockRejectedValue(new Error("Network error"));

    const { result } = renderHook(() => useConnections(1), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.error?.message).toBe("Network error");
  });
});

describe("useConnection", () => {
  it("calls api.getConnection with the provided id", async () => {
    const mockConnection = {
      data: { id: "conn-1", name: "My SFTP", protocol: "sftp" },
    };
    mockedApi.getConnection.mockResolvedValue(mockConnection);

    const { result } = renderHook(() => useConnection("conn-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.getConnection).toHaveBeenCalledWith("conn-1");
    expect(result.current.data).toEqual(mockConnection);
  });

  it("does not fetch when id is an empty string", async () => {
    const { result } = renderHook(() => useConnection(""), {
      wrapper: createWrapper(),
    });

    // With enabled: false, the query stays in pending/idle state and never fires
    expect(result.current.fetchStatus).toBe("idle");
    expect(mockedApi.getConnection).not.toHaveBeenCalled();
  });
});
