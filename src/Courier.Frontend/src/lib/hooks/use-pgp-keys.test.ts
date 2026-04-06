import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    listPgpKeys: vi.fn(),
    getPgpKey: vi.fn(),
  },
}));

import { api } from "../api";
import { usePgpKeys, usePgpKey } from "./use-pgp-keys";

const mockedApi = api as unknown as {
  listPgpKeys: ReturnType<typeof vi.fn>;
  getPgpKey: ReturnType<typeof vi.fn>;
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

describe("usePgpKeys", () => {
  it("calls api.listPgpKeys with page and default pageSize", async () => {
    const mockResponse = {
      data: [],
      totalCount: 0,
      page: 1,
      pageSize: 10,
    };
    mockedApi.listPgpKeys.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => usePgpKeys(1), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listPgpKeys).toHaveBeenCalledWith(1, 10, undefined);
    expect(result.current.data).toEqual(mockResponse);
  });

  it("calls api.listPgpKeys with custom pageSize", async () => {
    const mockResponse = {
      data: [],
      totalCount: 0,
      page: 2,
      pageSize: 25,
    };
    mockedApi.listPgpKeys.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => usePgpKeys(2, 25), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listPgpKeys).toHaveBeenCalledWith(2, 25, undefined);
    expect(result.current.data).toEqual(mockResponse);
  });

  it("passes filters to api.listPgpKeys", async () => {
    const filters = { search: "production", status: "active", keyType: "rsa" };
    const mockResponse = {
      data: [{ id: "pgp-1", name: "Prod Key" }],
      totalCount: 1,
      page: 1,
      pageSize: 10,
    };
    mockedApi.listPgpKeys.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => usePgpKeys(1, 10, filters), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listPgpKeys).toHaveBeenCalledWith(1, 10, filters);
    expect(result.current.data).toEqual(mockResponse);
  });

  it("returns error state when api call fails", async () => {
    mockedApi.listPgpKeys.mockRejectedValue(new Error("Network error"));

    const { result } = renderHook(() => usePgpKeys(1), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.error?.message).toBe("Network error");
  });
});

describe("usePgpKey", () => {
  it("calls api.getPgpKey with the provided id", async () => {
    const mockKey = {
      data: { id: "pgp-1", name: "My PGP Key", algorithm: "rsa4096" },
    };
    mockedApi.getPgpKey.mockResolvedValue(mockKey);

    const { result } = renderHook(() => usePgpKey("pgp-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.getPgpKey).toHaveBeenCalledWith("pgp-1");
    expect(result.current.data).toEqual(mockKey);
  });

  it("does not fetch when id is an empty string", async () => {
    const { result } = renderHook(() => usePgpKey(""), {
      wrapper: createWrapper(),
    });

    // With enabled: false, the query stays in pending/idle state and never fires
    expect(result.current.fetchStatus).toBe("idle");
    expect(mockedApi.getPgpKey).not.toHaveBeenCalled();
  });
});
