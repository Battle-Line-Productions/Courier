import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";
import type { ApiResponse, JobStepDto } from "../types";

vi.mock("../api", () => ({
  api: {
    listSteps: vi.fn(),
  },
}));

import { api } from "../api";
import { useJobSteps } from "./use-job-steps";

const mockedApi = api as {
  listSteps: ReturnType<typeof vi.fn>;
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

const fakeSteps: JobStepDto[] = [
  {
    id: "step-1",
    jobId: "job-1",
    stepOrder: 1,
    name: "Download File",
    typeKey: "sftp.download",
    configuration: '{"source_path": "/data/report.csv"}',
    timeoutSeconds: 600,
    alias: "download-report",
  },
  {
    id: "step-2",
    jobId: "job-1",
    stepOrder: 2,
    name: "Encrypt File",
    typeKey: "pgp.encrypt",
    configuration: '{"key_id": "key-1"}',
    timeoutSeconds: 300,
  },
];

const fakeStepsResponse: ApiResponse<JobStepDto[]> = {
  data: fakeSteps,
  timestamp: "2026-01-01T00:00:00Z",
  success: true,
};

beforeEach(() => {
  vi.clearAllMocks();
});

describe("useJobSteps", () => {
  it("calls api.listSteps with the jobId", async () => {
    mockedApi.listSteps.mockResolvedValue(fakeStepsResponse);
    const { result } = renderHook(() => useJobSteps("job-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockedApi.listSteps).toHaveBeenCalledWith("job-1");
  });

  it("returns steps data on success", async () => {
    mockedApi.listSteps.mockResolvedValue(fakeStepsResponse);
    const { result } = renderHook(() => useJobSteps("job-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.data).toHaveLength(2);
    expect(result.current.data?.data?.[0].name).toBe("Download File");
    expect(result.current.data?.data?.[1].typeKey).toBe("pgp.encrypt");
  });

  it("does not fetch when jobId is empty string", async () => {
    mockedApi.listSteps.mockResolvedValue(fakeStepsResponse);
    const { result } = renderHook(() => useJobSteps(""), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe("idle");
    expect(mockedApi.listSteps).not.toHaveBeenCalled();
  });

  it("has query key ['jobs', jobId, 'steps']", async () => {
    mockedApi.listSteps.mockResolvedValue(fakeStepsResponse);
    const { queryClient, wrapper } = createWrapperWithClient();

    const { result } = renderHook(() => useJobSteps("job-1"), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const cache = queryClient.getQueryCache().findAll();
    expect(cache[0]?.queryKey).toEqual(["jobs", "job-1", "steps"]);
  });

  it("returns error state when api rejects", async () => {
    mockedApi.listSteps.mockRejectedValue(new Error("Network error"));
    const { result } = renderHook(() => useJobSteps("job-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBeInstanceOf(Error);
  });

  it("re-fetches when jobId changes", async () => {
    mockedApi.listSteps.mockResolvedValue(fakeStepsResponse);
    const { queryClient, wrapper } = createWrapperWithClient();

    const { result, rerender } = renderHook(
      ({ jobId }: { jobId: string }) => useJobSteps(jobId),
      {
        wrapper,
        initialProps: { jobId: "job-1" },
      }
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockedApi.listSteps).toHaveBeenCalledWith("job-1");

    rerender({ jobId: "job-2" });

    await waitFor(() => expect(mockedApi.listSteps).toHaveBeenCalledWith("job-2"));
  });

  it("includes alias in returned step data", async () => {
    mockedApi.listSteps.mockResolvedValue(fakeStepsResponse);
    const { result } = renderHook(() => useJobSteps("job-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.data?.[0].alias).toBe("download-report");
    expect(result.current.data?.data?.[1].alias).toBeUndefined();
  });
});
