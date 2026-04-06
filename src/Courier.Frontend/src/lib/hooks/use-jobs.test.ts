import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";
import type { PagedApiResponse, JobDto, ApiResponse } from "../types";

vi.mock("../api", () => ({
  api: {
    listJobs: vi.fn(),
    getJob: vi.fn(),
  },
}));

import { api } from "../api";
import { useJobs, useJob, useAllJobs } from "./use-jobs";

const mockedApi = api as unknown as {
  listJobs: ReturnType<typeof vi.fn>;
  getJob: ReturnType<typeof vi.fn>;
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

const fakeJob: JobDto = {
  id: "job-1",
  name: "Test Job",
  description: "A test job",
  currentVersion: 1,
  isEnabled: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

const fakePagedResponse: PagedApiResponse<JobDto> = {
  data: [fakeJob],
  pagination: { page: 1, pageSize: 10, totalCount: 1, totalPages: 1 },
  timestamp: "2026-01-01T00:00:00Z",
  success: true,
};

const fakeSingleResponse: ApiResponse<JobDto> = {
  data: fakeJob,
  timestamp: "2026-01-01T00:00:00Z",
  success: true,
};

beforeEach(() => {
  vi.clearAllMocks();
});

describe("useJobs", () => {
  it("calls api.listJobs with correct page and pageSize", async () => {
    mockedApi.listJobs.mockResolvedValue(fakePagedResponse);
    const { result } = renderHook(() => useJobs(2, 25), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockedApi.listJobs).toHaveBeenCalledWith(2, 25, undefined);
  });

  it("returns paged job data on success", async () => {
    mockedApi.listJobs.mockResolvedValue(fakePagedResponse);
    const { result } = renderHook(() => useJobs(1, 10), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(fakePagedResponse);
    expect(result.current.data?.data).toHaveLength(1);
    expect(result.current.data?.data[0].name).toBe("Test Job");
  });

  it("uses default pageSize of 10", async () => {
    mockedApi.listJobs.mockResolvedValue(fakePagedResponse);
    const { result } = renderHook(() => useJobs(1), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockedApi.listJobs).toHaveBeenCalledWith(1, 10, undefined);
  });

  it("passes filters to api.listJobs", async () => {
    mockedApi.listJobs.mockResolvedValue(fakePagedResponse);
    const filters = { search: "deploy", tag: "prod" };
    const { result } = renderHook(() => useJobs(1, 10, filters), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockedApi.listJobs).toHaveBeenCalledWith(1, 10, filters);
  });

  it("includes page, pageSize, and filters in query key", async () => {
    mockedApi.listJobs.mockResolvedValue(fakePagedResponse);
    const filters = { search: "test" };
    const { queryClient, wrapper } = createWrapperWithClient();

    const { result } = renderHook(() => useJobs(3, 20, filters), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const cache = queryClient.getQueryCache().findAll();
    const queryKey = cache[0]?.queryKey;
    expect(queryKey).toEqual(["jobs", 3, 20, filters]);
  });

  it("returns error state when api.listJobs rejects", async () => {
    mockedApi.listJobs.mockRejectedValue(new Error("Network error"));
    const { result } = renderHook(() => useJobs(1, 10), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBeInstanceOf(Error);
  });
});

describe("useJob", () => {
  it("calls api.getJob with the correct id", async () => {
    mockedApi.getJob.mockResolvedValue(fakeSingleResponse);
    const { result } = renderHook(() => useJob("job-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockedApi.getJob).toHaveBeenCalledWith("job-1");
  });

  it("returns job data on success", async () => {
    mockedApi.getJob.mockResolvedValue(fakeSingleResponse);
    const { result } = renderHook(() => useJob("job-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.data?.name).toBe("Test Job");
  });

  it("does not fetch when id is empty string", async () => {
    mockedApi.getJob.mockResolvedValue(fakeSingleResponse);
    const { result } = renderHook(() => useJob(""), {
      wrapper: createWrapper(),
    });

    // Should stay in pending state since the query is disabled
    expect(result.current.fetchStatus).toBe("idle");
    expect(mockedApi.getJob).not.toHaveBeenCalled();
  });

  it("has query key of ['jobs', id]", async () => {
    mockedApi.getJob.mockResolvedValue(fakeSingleResponse);
    const { queryClient, wrapper } = createWrapperWithClient();

    const { result } = renderHook(() => useJob("job-42"), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const cache = queryClient.getQueryCache().findAll();
    expect(cache[0]?.queryKey).toEqual(["jobs", "job-42"]);
  });
});

describe("useAllJobs", () => {
  it("calls api.listJobs with page 1 and pageSize 200", async () => {
    mockedApi.listJobs.mockResolvedValue(fakePagedResponse);
    const { result } = renderHook(() => useAllJobs(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockedApi.listJobs).toHaveBeenCalledWith(1, 200);
  });

  it("returns paged response data", async () => {
    mockedApi.listJobs.mockResolvedValue(fakePagedResponse);
    const { result } = renderHook(() => useAllJobs(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(fakePagedResponse);
  });

  it("has query key of ['jobs', 'all']", async () => {
    mockedApi.listJobs.mockResolvedValue(fakePagedResponse);
    const { queryClient, wrapper } = createWrapperWithClient();

    const { result } = renderHook(() => useAllJobs(), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const cache = queryClient.getQueryCache().findAll();
    expect(cache[0]?.queryKey).toEqual(["jobs", "all"]);
  });
});
