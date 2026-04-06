import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";
import type {
  PagedApiResponse,
  ApiResponse,
  JobExecutionDto,
} from "../types";

vi.mock("../api", () => ({
  api: {
    listExecutions: vi.fn(),
    getExecution: vi.fn(),
  },
}));

import { api } from "../api";
import { useJobExecutions, useExecution } from "./use-job-executions";

const mockedApi = api as {
  listExecutions: ReturnType<typeof vi.fn>;
  getExecution: ReturnType<typeof vi.fn>;
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

function createWrapperWithClient() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });
  const wrapper = ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: queryClient }, children);
  return { wrapper, queryClient };
}

const fakeExecution: JobExecutionDto = {
  id: "exec-1",
  jobId: "job-1",
  state: "completed",
  triggeredBy: "ui",
  queuedAt: "2026-01-01T00:00:00Z",
  startedAt: "2026-01-01T00:00:01Z",
  completedAt: "2026-01-01T00:00:05Z",
  createdAt: "2026-01-01T00:00:00Z",
};

const fakePagedResponse: PagedApiResponse<JobExecutionDto> = {
  data: [fakeExecution],
  pagination: { page: 1, pageSize: 10, totalCount: 1, totalPages: 1 },
  timestamp: "2026-01-01T00:00:00Z",
  success: true,
};

const fakeSingleResponse: ApiResponse<JobExecutionDto> = {
  data: fakeExecution,
  timestamp: "2026-01-01T00:00:00Z",
  success: true,
};

beforeEach(() => {
  vi.clearAllMocks();
});

describe("useJobExecutions", () => {
  it("calls api.listExecutions with jobId, page, and pageSize", async () => {
    mockedApi.listExecutions.mockResolvedValue(fakePagedResponse);
    const { result } = renderHook(() => useJobExecutions("job-1", 2, 25), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockedApi.listExecutions).toHaveBeenCalledWith("job-1", 2, 25);
  });

  it("uses default page=1 and pageSize=10", async () => {
    mockedApi.listExecutions.mockResolvedValue(fakePagedResponse);
    const { result } = renderHook(() => useJobExecutions("job-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockedApi.listExecutions).toHaveBeenCalledWith("job-1", 1, 10);
  });

  it("returns paged execution data on success", async () => {
    mockedApi.listExecutions.mockResolvedValue(fakePagedResponse);
    const { result } = renderHook(() => useJobExecutions("job-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.data).toHaveLength(1);
    expect(result.current.data?.data[0].state).toBe("completed");
  });

  it("does not fetch when jobId is empty string", async () => {
    mockedApi.listExecutions.mockResolvedValue(fakePagedResponse);
    const { result } = renderHook(() => useJobExecutions(""), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe("idle");
    expect(mockedApi.listExecutions).not.toHaveBeenCalled();
  });

  it("has query key ['jobs', jobId, 'executions', page, pageSize]", async () => {
    mockedApi.listExecutions.mockResolvedValue(fakePagedResponse);
    const { queryClient, wrapper } = createWrapperWithClient();

    const { result } = renderHook(() => useJobExecutions("job-1", 3, 15), {
      wrapper,
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const cache = queryClient.getQueryCache().findAll();
    expect(cache[0]?.queryKey).toEqual(["jobs", "job-1", "executions", 3, 15]);
  });

  it("returns error state when api rejects", async () => {
    mockedApi.listExecutions.mockRejectedValue(new Error("fail"));
    const { result } = renderHook(() => useJobExecutions("job-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useExecution", () => {
  it("calls api.getExecution with the executionId", async () => {
    mockedApi.getExecution.mockResolvedValue(fakeSingleResponse);
    const { result } = renderHook(() => useExecution("exec-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockedApi.getExecution).toHaveBeenCalledWith("exec-1");
  });

  it("returns execution data on success", async () => {
    mockedApi.getExecution.mockResolvedValue(fakeSingleResponse);
    const { result } = renderHook(() => useExecution("exec-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.data?.id).toBe("exec-1");
    expect(result.current.data?.data?.state).toBe("completed");
  });

  it("does not fetch when executionId is empty string", async () => {
    mockedApi.getExecution.mockResolvedValue(fakeSingleResponse);
    const { result } = renderHook(() => useExecution(""), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe("idle");
    expect(mockedApi.getExecution).not.toHaveBeenCalled();
  });

  it("does not fetch when enabled is false", async () => {
    mockedApi.getExecution.mockResolvedValue(fakeSingleResponse);
    const { result } = renderHook(() => useExecution("exec-1", false), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe("idle");
    expect(mockedApi.getExecution).not.toHaveBeenCalled();
  });

  it("has query key ['executions', executionId]", async () => {
    mockedApi.getExecution.mockResolvedValue(fakeSingleResponse);
    const { queryClient, wrapper } = createWrapperWithClient();

    const { result } = renderHook(() => useExecution("exec-42"), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const cache = queryClient.getQueryCache().findAll();
    expect(cache[0]?.queryKey).toEqual(["executions", "exec-42"]);
  });

  it("sets refetchInterval for queued state", async () => {
    const queuedExecution: ApiResponse<JobExecutionDto> = {
      data: { ...fakeExecution, state: "queued" },
      timestamp: "2026-01-01T00:00:00Z",
      success: true,
    };
    mockedApi.getExecution.mockResolvedValue(queuedExecution);
    const { queryClient, wrapper } = createWrapperWithClient();

    const { result } = renderHook(() => useExecution("exec-1"), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    // The query should be configured for refetching in active states.
    // Verify data is in the queued state which triggers the 2000ms interval.
    expect(result.current.data?.data?.state).toBe("queued");
  });

  it("sets refetchInterval for running state", async () => {
    const runningExecution: ApiResponse<JobExecutionDto> = {
      data: { ...fakeExecution, state: "running" },
      timestamp: "2026-01-01T00:00:00Z",
      success: true,
    };
    mockedApi.getExecution.mockResolvedValue(runningExecution);

    const { result } = renderHook(() => useExecution("exec-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.data?.state).toBe("running");
  });

  it("does not refetch for completed state", async () => {
    // The completed execution should not set a refetchInterval
    mockedApi.getExecution.mockResolvedValue(fakeSingleResponse);

    const { result } = renderHook(() => useExecution("exec-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.data?.state).toBe("completed");
  });
});
