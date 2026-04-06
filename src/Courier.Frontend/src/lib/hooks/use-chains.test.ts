import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    listChains: vi.fn(),
    getChain: vi.fn(),
    listChainExecutions: vi.fn(),
  },
}));

import { api } from "../api";
import { useChains, useChain, useChainExecutions } from "./use-chains";

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

describe("useChains", () => {
  it("calls api.listChains with default page and pageSize", async () => {
    const mockResponse = { data: [], totalCount: 0, page: 1, pageSize: 10 };
    vi.mocked(api.listChains).mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useChains(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(api.listChains).toHaveBeenCalledWith(1, 10, undefined);
    expect(result.current.data).toEqual(mockResponse);
  });

  it("calls api.listChains with custom page, pageSize, and filters", async () => {
    const mockResponse = { data: [], totalCount: 0, page: 2, pageSize: 25 };
    const filters = { search: "deploy", tag: "prod" };
    vi.mocked(api.listChains).mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useChains(2, 25, filters), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(api.listChains).toHaveBeenCalledWith(2, 25, filters);
  });
});

describe("useChain", () => {
  it("calls api.getChain when id is provided", async () => {
    const mockChain = { data: { id: "abc-123", name: "Test Chain" } };
    vi.mocked(api.getChain).mockResolvedValue(mockChain);

    const { result } = renderHook(() => useChain("abc-123"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(api.getChain).toHaveBeenCalledWith("abc-123");
    expect(result.current.data).toEqual(mockChain);
  });

  it("does not call api.getChain when id is empty", async () => {
    const { result } = renderHook(() => useChain(""), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe("idle");
    expect(api.getChain).not.toHaveBeenCalled();
  });
});

describe("useChainExecutions", () => {
  it("calls api.listChainExecutions when chainId is provided", async () => {
    const mockResponse = { data: [], totalCount: 0, page: 1, pageSize: 10 };
    vi.mocked(api.listChainExecutions).mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useChainExecutions("chain-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(api.listChainExecutions).toHaveBeenCalledWith("chain-1", 1, 10);
    expect(result.current.data).toEqual(mockResponse);
  });

  it("calls api.listChainExecutions with custom page and pageSize", async () => {
    const mockResponse = { data: [], totalCount: 0, page: 3, pageSize: 5 };
    vi.mocked(api.listChainExecutions).mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useChainExecutions("chain-1", 3, 5), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(api.listChainExecutions).toHaveBeenCalledWith("chain-1", 3, 5);
  });

  it("does not call api.listChainExecutions when chainId is empty", async () => {
    const { result } = renderHook(() => useChainExecutions(""), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe("idle");
    expect(api.listChainExecutions).not.toHaveBeenCalled();
  });
});
