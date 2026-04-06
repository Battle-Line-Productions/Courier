import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    listChainSchedules: vi.fn(),
    createChainSchedule: vi.fn(),
    updateChainSchedule: vi.fn(),
    deleteChainSchedule: vi.fn(),
  },
}));

import { api } from "../api";
import {
  useChainSchedules,
  useCreateChainSchedule,
  useUpdateChainSchedule,
  useDeleteChainSchedule,
} from "./use-chain-schedules";

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

describe("useChainSchedules", () => {
  it("calls api.listChainSchedules when chainId is provided", async () => {
    const mockSchedules = { data: [{ id: "sched-1", scheduleType: "cron" }], timestamp: "2026-01-01T00:00:00Z", success: true } as any;
    vi.mocked(api.listChainSchedules).mockResolvedValue(mockSchedules);

    const { result } = renderHook(() => useChainSchedules("chain-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(api.listChainSchedules).toHaveBeenCalledWith("chain-1");
    expect(result.current.data).toEqual(mockSchedules);
  });

  it("does not call api.listChainSchedules when chainId is empty", async () => {
    const { result } = renderHook(() => useChainSchedules(""), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe("idle");
    expect(api.listChainSchedules).not.toHaveBeenCalled();
  });
});

describe("useCreateChainSchedule", () => {
  it("calls api.createChainSchedule and invalidates schedules query", async () => {
    const mockSchedule = { data: { id: "sched-new", scheduleType: "cron" }, timestamp: "2026-01-01T00:00:00Z", success: true } as any;
    vi.mocked(api.createChainSchedule).mockResolvedValue(mockSchedule);
    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useCreateChainSchedule("chain-1"), {
      wrapper,
    });

    await act(async () => {
      result.current.mutate({
        scheduleType: "cron",
        cronExpression: "0 0 * * *",
        isEnabled: true,
      });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(api.createChainSchedule).toHaveBeenCalledWith("chain-1", {
      scheduleType: "cron",
      cronExpression: "0 0 * * *",
      isEnabled: true,
    });
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["chains", "chain-1", "schedules"],
    });
  });
});

describe("useUpdateChainSchedule", () => {
  it("calls api.updateChainSchedule with chainId, scheduleId, and data", async () => {
    const mockSchedule = { data: { id: "sched-1", isEnabled: false }, timestamp: "2026-01-01T00:00:00Z", success: true } as any;
    vi.mocked(api.updateChainSchedule).mockResolvedValue(mockSchedule);
    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useUpdateChainSchedule("chain-1"), {
      wrapper,
    });

    await act(async () => {
      result.current.mutate({
        scheduleId: "sched-1",
        data: { isEnabled: false, cronExpression: "0 6 * * *" },
      });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(api.updateChainSchedule).toHaveBeenCalledWith(
      "chain-1",
      "sched-1",
      { isEnabled: false, cronExpression: "0 6 * * *" },
    );
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["chains", "chain-1", "schedules"],
    });
  });
});

describe("useDeleteChainSchedule", () => {
  it("calls api.deleteChainSchedule and invalidates schedules query", async () => {
    vi.mocked(api.deleteChainSchedule).mockResolvedValue({ data: undefined, timestamp: "2026-01-01T00:00:00Z", success: true } as any);
    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useDeleteChainSchedule("chain-1"), {
      wrapper,
    });

    await act(async () => {
      result.current.mutate("sched-to-delete");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(api.deleteChainSchedule).toHaveBeenCalledWith(
      "chain-1",
      "sched-to-delete",
    );
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["chains", "chain-1", "schedules"],
    });
  });
});
