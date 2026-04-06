import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    listSshKeys: vi.fn(),
    getSshKey: vi.fn(),
  },
}));

import { api } from "../api";
import { useSshKeys, useSshKey } from "./use-ssh-keys";

const mockedApi = api as unknown as {
  listSshKeys: ReturnType<typeof vi.fn>;
  getSshKey: ReturnType<typeof vi.fn>;
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

describe("useSshKeys", () => {
  it("calls api.listSshKeys with page and default pageSize", async () => {
    const mockResponse = {
      data: [],
      totalCount: 0,
      page: 1,
      pageSize: 10,
    };
    mockedApi.listSshKeys.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useSshKeys(1), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listSshKeys).toHaveBeenCalledWith(1, 10, undefined);
    expect(result.current.data).toEqual(mockResponse);
  });

  it("calls api.listSshKeys with custom pageSize", async () => {
    const mockResponse = {
      data: [],
      totalCount: 0,
      page: 3,
      pageSize: 50,
    };
    mockedApi.listSshKeys.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useSshKeys(3, 50), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listSshKeys).toHaveBeenCalledWith(3, 50, undefined);
    expect(result.current.data).toEqual(mockResponse);
  });

  it("passes filters to api.listSshKeys", async () => {
    const filters = { search: "deploy", status: "active", keyType: "ed25519" };
    const mockResponse = {
      data: [{ id: "ssh-1", name: "Deploy Key" }],
      totalCount: 1,
      page: 1,
      pageSize: 10,
    };
    mockedApi.listSshKeys.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useSshKeys(1, 10, filters), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listSshKeys).toHaveBeenCalledWith(1, 10, filters);
    expect(result.current.data).toEqual(mockResponse);
  });

  it("returns error state when api call fails", async () => {
    mockedApi.listSshKeys.mockRejectedValue(new Error("Network error"));

    const { result } = renderHook(() => useSshKeys(1), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.error?.message).toBe("Network error");
  });
});

describe("useSshKey", () => {
  it("calls api.getSshKey with the provided id", async () => {
    const mockKey = {
      data: { id: "ssh-1", name: "My SSH Key", keyType: "ed25519" },
    };
    mockedApi.getSshKey.mockResolvedValue(mockKey);

    const { result } = renderHook(() => useSshKey("ssh-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.getSshKey).toHaveBeenCalledWith("ssh-1");
    expect(result.current.data).toEqual(mockKey);
  });

  it("does not fetch when id is an empty string", async () => {
    const { result } = renderHook(() => useSshKey(""), {
      wrapper: createWrapper(),
    });

    // With enabled: false, the query stays in pending/idle state and never fires
    expect(result.current.fetchStatus).toBe("idle");
    expect(mockedApi.getSshKey).not.toHaveBeenCalled();
  });
});
