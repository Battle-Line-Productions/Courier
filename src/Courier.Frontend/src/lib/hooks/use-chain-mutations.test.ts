import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    createChain: vi.fn(),
    updateChain: vi.fn(),
    deleteChain: vi.fn(),
    replaceChainMembers: vi.fn(),
    triggerChain: vi.fn(),
  },
}));

import { api } from "../api";
import {
  useCreateChain,
  useUpdateChain,
  useDeleteChain,
  useReplaceChainMembers,
  useTriggerChain,
} from "./use-chain-mutations";

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

describe("useCreateChain", () => {
  it("calls api.createChain and invalidates chains queries", async () => {
    const mockChain = { data: { id: "new-1", name: "New Chain" } };
    vi.mocked(api.createChain).mockResolvedValue(mockChain);
    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useCreateChain(), { wrapper });

    await act(async () => {
      result.current.mutate({ name: "New Chain", description: "desc" });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(api.createChain).toHaveBeenCalledWith({
      name: "New Chain",
      description: "desc",
    });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["chains"] });
  });
});

describe("useUpdateChain", () => {
  it("calls api.updateChain with id and data, then invalidates chains queries", async () => {
    const mockChain = { data: { id: "chain-1", name: "Updated" } };
    vi.mocked(api.updateChain).mockResolvedValue(mockChain);
    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useUpdateChain("chain-1"), { wrapper });

    await act(async () => {
      result.current.mutate({ name: "Updated", description: "new desc" });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(api.updateChain).toHaveBeenCalledWith("chain-1", {
      name: "Updated",
      description: "new desc",
    });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["chains"] });
  });
});

describe("useDeleteChain", () => {
  it("calls api.deleteChain and invalidates chains queries", async () => {
    vi.mocked(api.deleteChain).mockResolvedValue({ data: undefined });
    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useDeleteChain(), { wrapper });

    await act(async () => {
      result.current.mutate("chain-to-delete");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(api.deleteChain).toHaveBeenCalledWith("chain-to-delete");
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["chains"] });
  });
});

describe("useReplaceChainMembers", () => {
  it("calls api.replaceChainMembers and invalidates the specific chain query", async () => {
    const mockResponse = { data: { id: "chain-1", name: "Chain" } };
    vi.mocked(api.replaceChainMembers).mockResolvedValue(mockResponse);
    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useReplaceChainMembers("chain-1"), {
      wrapper,
    });

    const members = [
      { jobId: "job-1", sequence: 1 },
      { jobId: "job-2", sequence: 2, delaySeconds: 30 },
    ];

    await act(async () => {
      result.current.mutate({ members });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(api.replaceChainMembers).toHaveBeenCalledWith("chain-1", {
      members,
    });
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["chains", "chain-1"],
    });
  });
});

describe("useTriggerChain", () => {
  it("calls api.triggerChain and invalidates chain executions", async () => {
    const mockExecution = { data: { id: "exec-1", status: "queued" } };
    vi.mocked(api.triggerChain).mockResolvedValue(mockExecution);
    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useTriggerChain("chain-1"), {
      wrapper,
    });

    await act(async () => {
      result.current.mutate();
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(api.triggerChain).toHaveBeenCalledWith("chain-1");
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["chains", "chain-1", "executions"],
    });
  });
});
